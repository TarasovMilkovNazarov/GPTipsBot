using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Exceptions;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using OpenAI.Interfaces;
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.Web;

public class WebChatService(
    UserService userService,
    MessageRepository messageRepository,
    ContextWindow contextWindow,
    IOpenAIService openAiService,
    BotSettingsRepository botSettingsRepository)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async IAsyncEnumerable<string> StreamChatAsync(
        long userId,
        string text,
        bool newConversation,
        long? continueContextId,
        bool isGuest,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return Event("error", new { message = "Empty message" });
            yield break;
        }

        var chatId = userId;
        var userKey = new UserChatKey(userId, chatId);
        var model = userService.GetPreferredGptModel(userId);
        var hold = await userService.TryReserveGptAsync(userId, model);
        if (hold is null)
        {
            var freeQuotaExhausted = model.AllowFreeQuota;
            yield return Event("error", new
            {
                code = "quota",
                suggestRegister = isGuest && freeQuotaExhausted,
                message = freeQuotaExhausted
                    ? isGuest
                        ? "Free GPT quota exhausted. Sign up to restore free limits."
                        : "Free GPT quota exhausted. Top up Stars or wait for daily reset."
                    : $"Model {model.DisplayName} requires {model.StarsCost} Stars.",
            });
            yield break;
        }

        var channel = Channel.CreateUnbounded<string>();
        var produce = ProduceAsync(
            channel.Writer,
            userKey,
            text.Trim(),
            newConversation,
            continueContextId,
            isGuest,
            model,
            hold.Id,
            cancellationToken);

        await foreach (var chunk in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return chunk;
        }

        await produce;
    }

    private async Task ProduceAsync(
        ChannelWriter<string> writer,
        UserChatKey userKey,
        string text,
        bool newConversation,
        long? continueContextId,
        bool isGuest,
        GptModelOption model,
        long holdId,
        CancellationToken cancellationToken)
    {
        var confirmed = false;
        try
        {
            // Guests share one thread: always continue the existing context if present.
            long? forceContextId;
            if (isGuest)
            {
                forceContextId = continueContextId is > 0
                    ? continueContextId
                    : messageRepository.GetLastContext(userKey.Id, userKey.ChatId);
            }
            else
            {
                forceContextId = !newConversation && continueContextId is > 0
                    ? continueContextId
                    : null;
            }

            var userMessage = new MessageDto(userKey)
            {
                Text = text,
                Role = MessageOwner.User,
                ContextBound = true,
                NewContext = forceContextId is null,
                BotMessageType = BotMessageType.ChatGptPrompt,
            };

            if (forceContextId is not null)
            {
                userMessage.NewContext = false;
            }

            var saved = await messageRepository.AddAsync(userMessage, forceContextId: forceContextId);
            var contextId = saved.ContextId;

            ChatMessage[] messages;
            if (userMessage.NewContext || contextId is null)
            {
                messages = [new ChatMessage("user", text)];
            }
            else
            {
                messages = contextWindow.GetContext(userKey, contextId.Value);
            }

            await writer.WriteAsync(
                Event("start", new { contextId, modelId = model.Id, modelName = model.DisplayName }),
                cancellationToken);

            var assistantText = new StringBuilder();
            await foreach (var chunk in openAiService.ChatCompletion.CreateCompletionAsStream(
                               new ChatCompletionCreateRequest { Messages = messages, Model = model.Id },
                               model.Id,
                               cancellationToken: cancellationToken))
            {
                if (!chunk.Successful)
                {
                    await writer.WriteAsync(
                        Event("error", new
                        {
                            code = chunk.Error?.Code,
                            message = chunk.Error?.Message ?? "OpenAI request failed",
                        }),
                        cancellationToken);
                    return;
                }

                var delta = chunk.Choices?.FirstOrDefault()?.Delta?.Content;
                if (string.IsNullOrEmpty(delta))
                {
                    continue;
                }

                assistantText.Append(delta);
                await writer.WriteAsync(Event("token", new { text = delta }), cancellationToken);
            }

            var reply = assistantText.ToString();
            if (string.IsNullOrWhiteSpace(reply))
            {
                await writer.WriteAsync(Event("error", new { message = "Empty model response" }), cancellationToken);
                return;
            }

            var assistantMessage = new MessageDto(userKey)
            {
                Text = reply,
                Role = MessageOwner.Assistant,
                ContextBound = true,
                NewContext = false,
            };
            var savedAssistant = await messageRepository.AddAsync(
                assistantMessage,
                saved,
                forceContextId: contextId);
            await userService.ConfirmAsync(holdId);
            confirmed = true;

            await writer.WriteAsync(
                Event("done", new
                {
                    contextId = savedAssistant.ContextId ?? contextId,
                    messageId = savedAssistant.Id,
                    text = reply,
                }),
                cancellationToken);
        }
        catch (ClientException ex)
        {
            await writer.WriteAsync(Event("error", new { message = ex.Message }), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            await writer.WriteAsync(Event("error", new { message = "Cancelled" }), CancellationToken.None);
        }
        catch (Exception)
        {
            await writer.WriteAsync(Event("error", new { message = "Something went wrong" }), CancellationToken.None);
        }
        finally
        {
            if (!confirmed)
            {
                await userService.ReleaseAsync(holdId);
            }

            writer.TryComplete();
        }
    }

    public Task SetModelAsync(long userId, string modelId, string language = "en")
    {
        var model = GptModelCatalog.GetRequired(modelId);
        botSettingsRepository.SetPreferredGptModel(userId, model.Id, language);
        return Task.CompletedTask;
    }

    private static string Event(string type, object payload) =>
        $"data: {JsonSerializer.Serialize(new { type, payload }, JsonOptions)}\n\n";
}
