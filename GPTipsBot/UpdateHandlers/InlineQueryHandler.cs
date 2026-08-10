using System.Diagnostics;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.Inline;
using GPTipsBot.Services.YandexCloud.Workflow;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers;

public class InlineQueryHandler(
    ITelegramBotClient botClient,
    InlinePendingStore pendingStore,
    UserService userService,
    IGpt gptService,
    IJobService jobService,
    ImageGenerationWorkflowService imageGenerationWorkflowService,
    ILogger<InlineQueryHandler> logger)
{
    public const string CallbackPrefix = "iq:";

    private const string AskPrefix = "ask";
    private const string ImagePrefix = "image";
    private const string AskSystemPrompt =
        "Answer the user question clearly and concisely. Match the user's language.";

    public static bool IsInlineCallback(string? data) =>
        !string.IsNullOrEmpty(data) && data.StartsWith(CallbackPrefix, StringComparison.Ordinal);

    public async Task HandleAsync(UpdateDecorator update)
    {
        if (update.InlineQuery != null)
        {
            await AnswerQueryAsync(update);
            return;
        }

        if (update.ChosenInlineResult != null)
        {
            await HandleChosenAsync(update);
        }
    }

    public async Task HandleCallbackAsync(UpdateDecorator update)
    {
        var callback = update.CallbackQuery!;
        var data = callback.Data ?? string.Empty;
        if (!data.StartsWith(CallbackPrefix, StringComparison.Ordinal))
        {
            return;
        }

        var pendingId = data[CallbackPrefix.Length..];
        var inlineMessageId = callback.InlineMessageId;

        if (string.IsNullOrEmpty(inlineMessageId))
        {
            await botClient.AnswerCallbackQuery(callback.Id, BotResponse.SomethingWentWrong, showAlert: true);
            return;
        }

        if (!pendingStore.TryTake(pendingId, update.TelegramUserId, out var pending) || pending is null)
        {
            await botClient.AnswerCallbackQuery(callback.Id, BotResponse.InlineResultExpired, showAlert: true);
            return;
        }

        await botClient.AnswerCallbackQuery(callback.Id);
        await RunPendingAsync(update, inlineMessageId, pending);
    }

    private async Task AnswerQueryAsync(UpdateDecorator update)
    {
        var query = update.InlineQuery!;
        var text = (query.Query ?? string.Empty).Trim();
        var results = BuildResults(update.TelegramUserId, text);

        await botClient.AnswerInlineQuery(
            query.Id,
            results,
            cacheTime: 0,
            isPersonal: true);
    }

    private IEnumerable<InlineQueryResult> BuildResults(long telegramUserId, string text)
    {
        if (TryParse(text, AskPrefix, out var question))
        {
            yield return CreateActionResult(
                pendingStore.Create(InlinePendingKind.Ask, telegramUserId, question),
                string.Format(BotResponse.InlineAskTitle, Truncate(question, 48)),
                BotResponse.InlineAskDescription,
                string.Format(BotResponse.InlineAskPending, Truncate(question, 200)));
            yield break;
        }

        if (TryParse(text, ImagePrefix, out var prompt))
        {
            if (prompt.Length > ImageGeneratorHandler.ImageTextDescriptionLimit)
            {
                yield return CreateInfoResult(
                    "image_too_long",
                    BotResponse.InlineImageTitleShort,
                    string.Format(BotResponse.ImageDescriptionLimitWarning, ImageGeneratorHandler.ImageTextDescriptionLimit),
                    string.Format(BotResponse.ImageDescriptionLimitWarning, ImageGeneratorHandler.ImageTextDescriptionLimit));
                yield break;
            }

            yield return CreateActionResult(
                pendingStore.Create(InlinePendingKind.Image, telegramUserId, prompt),
                string.Format(BotResponse.InlineImageTitle, Truncate(prompt, 48)),
                BotResponse.InlineImageDescription,
                string.Format(BotResponse.InlineImagePending, Truncate(prompt, 200)));
            yield break;
        }

        yield return CreateInfoResult(
            "help_ask",
            BotResponse.InlineHelpAskTitle,
            BotResponse.InlineHelpAskDescription,
            BotResponse.InlineHelpAskMessage);

        yield return CreateInfoResult(
            "help_image",
            BotResponse.InlineHelpImageTitle,
            BotResponse.InlineHelpImageDescription,
            BotResponse.InlineHelpImageMessage);
    }

    private async Task HandleChosenAsync(UpdateDecorator update)
    {
        var chosen = update.ChosenInlineResult!;
        if (chosen.ResultId is "help_ask" or "help_image" or "image_too_long")
        {
            return;
        }

        if (string.IsNullOrEmpty(chosen.InlineMessageId))
        {
            logger.LogWarning(
                "ChosenInlineResult without inline_message_id (enable Inline Feedback in BotFather). ResultId={ResultId}",
                chosen.ResultId);
            return;
        }

        // Prefer auto-start when BotFather inline feedback is enabled.
        // If the user already pressed the callback button, TryTake returns false — ignore.
        if (!pendingStore.TryTake(chosen.ResultId, update.TelegramUserId, out var pending) || pending is null)
        {
            return;
        }

        await RunPendingAsync(update, chosen.InlineMessageId, pending);
    }

    private Task RunPendingAsync(UpdateDecorator update, string inlineMessageId, InlinePendingRequest pending) =>
        pending.Kind switch
        {
            InlinePendingKind.Ask => HandleAskChosenAsync(update, inlineMessageId, pending.Payload),
            InlinePendingKind.Image => HandleImageChosenAsync(update, inlineMessageId, pending.Payload),
            _ => Task.CompletedTask
        };

    private async Task HandleAskChosenAsync(UpdateDecorator update, string inlineMessageId, string question)
    {
        var userId = update.UserChatKey.Id;
        var model = userService.GetPreferredGptModel(userId);
        var hold = await userService.TryReserveGptAsync(userId, model);
        if (hold is null)
        {
            var message = model.AllowFreeQuota
                ? await FormatOutOfQuotaAsync()
                : string.Format(
                    BotResponse.ModelNeedsBalance,
                    model.DisplayName,
                    model.StarsCost,
                    GptModelCatalog.Default.DisplayName);

            await botClient.EditMessageText(inlineMessageId, message, replyMarkup: null);
            return;
        }

        var confirmed = false;
        try
        {
            var sw = Stopwatch.StartNew();
            var response = await gptService.SendOneOffAsync(
                AskSystemPrompt,
                question,
                CancellationToken.None,
                model.Id);
            sw.Stop();

            var answer = response.Choices.FirstOrDefault()?.Message.Content?.Trim();
            if (string.IsNullOrWhiteSpace(answer))
            {
                await botClient.EditMessageText(inlineMessageId, BotResponse.SomethingWentWrong, replyMarkup: null);
                return;
            }

            logger.LogInformation(
                "Inline ask for '{Prompt}' took {Duration}s",
                Truncate(question, 30),
                sw.Elapsed.TotalSeconds);

            await EditInlineTextAsync(inlineMessageId, answer);
            await userService.ConfirmAsync(hold.Id);
            confirmed = true;
        }
        catch (ChatGptException ex)
        {
            logger.LogError(ex, "Inline ask failed");
            await botClient.EditMessageText(inlineMessageId, BotResponse.SomethingWentWrong, replyMarkup: null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inline ask failed unexpectedly");
            await botClient.EditMessageText(inlineMessageId, BotResponse.SomethingWentWrong, replyMarkup: null);
        }
        finally
        {
            if (!confirmed)
            {
                await userService.ReleaseAsync(hold.Id);
            }
        }
    }

    private async Task HandleImageChosenAsync(UpdateDecorator update, string inlineMessageId, string prompt)
    {
        var userId = update.UserChatKey.Id;
        var hold = await userService.TryReserveImageAsync(userId);
        if (hold is null)
        {
            await botClient.EditMessageText(inlineMessageId, await FormatOutOfQuotaAsync(), replyMarkup: null);
            return;
        }

        try
        {
            await imageGenerationWorkflowService.StartAsync(new ImageGenerationWorkflowData
            {
                ChatId = update.UserChatKey.ChatId,
                ReplyChatId = update.UserChatKey.ChatId,
                UserId = userId,
                Prompt = prompt,
                IsSquare = false,
                InlineMessageId = inlineMessageId,
                PaymentHoldId = hold.Id,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start inline image generation");
            await userService.ReleaseAsync(hold.Id);
            await botClient.EditMessageText(inlineMessageId, BotResponse.SomethingWentWrong, replyMarkup: null);
        }
    }

    private async Task<string> FormatOutOfQuotaAsync()
    {
        var nextExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
        if (!nextExec.HasValue || nextExec.Value < DateTimeOffset.UtcNow)
        {
            return BotResponse.SimpleNoFreeRequests;
        }

        var timeTillRefresh = nextExec.Value - DateTimeOffset.UtcNow;
        return string.Format(BotResponse.TimeNoFreeRequests, timeTillRefresh);
    }

    private async Task EditInlineTextAsync(string inlineMessageId, string text)
    {
        var parts = SplitIfTooLong(text);
        var first = parts[0];
        try
        {
            await botClient.EditMessageText(inlineMessageId, first, replyMarkup: null);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to edit inline text; retrying truncated");
            await botClient.EditMessageText(inlineMessageId, Truncate(first, 4000), replyMarkup: null);
        }
    }

    private static InlineQueryResultArticle CreateActionResult(
        InlinePendingRequest pending,
        string title,
        string description,
        string pendingMessage)
    {
        return new InlineQueryResultArticle(
            pending.Id,
            title,
            new InputTextMessageContent(pendingMessage))
        {
            Description = description,
            ReplyMarkup = ActionKeyboard(pending.Id),
        };
    }

    private static InlineQueryResultArticle CreateInfoResult(
        string id,
        string title,
        string description,
        string message)
    {
        return new InlineQueryResultArticle(
            id,
            title,
            new InputTextMessageContent(message))
        {
            Description = description,
            ReplyMarkup = BotLinkKeyboard(),
        };
    }

    private static InlineKeyboardMarkup ActionKeyboard(string pendingId)
    {
        return new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData(BotResponse.InlineRunButton, CallbackPrefix + pendingId));
    }

    private static InlineKeyboardMarkup BotLinkKeyboard()
    {
        var botName = string.IsNullOrWhiteSpace(AppConfig.BotName) ? "GPTipsBot" : AppConfig.BotName.TrimStart('@');
        return new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl("GPTipsBot", $"https://t.me/{botName}"));
    }

    /// <summary>
    /// Accepts "ask …", "/ask …", "image …", "/image …" (case-insensitive).
    /// </summary>
    public static bool TryParse(string text, string command, out string argument)
    {
        argument = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        var candidates = new[] { command, "/" + command };
        foreach (var candidate in candidates)
        {
            if (text.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (text.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase))
            {
                argument = text[(candidate.Length + 1)..].Trim();
                return !string.IsNullOrEmpty(argument);
            }
        }

        return false;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value;
        }

        return value[..(max - 1)] + "…";
    }

    private static List<string> SplitIfTooLong(string text, int max = 4096)
    {
        if (text.Length <= max)
        {
            return [text];
        }

        var parts = new List<string>();
        for (var i = 0; i < text.Length; i += max)
        {
            parts.Add(text.Substring(i, Math.Min(max, text.Length - i)));
        }

        return parts;
    }
}
