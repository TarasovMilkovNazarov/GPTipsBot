using System.Text.Json;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.SharedModels;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.Services;

/// <summary>
/// Tools the main chat model can invoke mid-conversation — the "true agent loop": the model
/// itself decides to call a tool, we execute it for real (including billing), and feed the
/// result back to the model via <see cref="ChatMessage.FromTool"/> for a final, integrated reply.
/// See <see cref="ChatGptService.SendMessage"/> for the loop that drives this.
/// </summary>
public class ChatToolExecutor(
    ITelegramBotClient botClient,
    ImageCreatorService imageCreatorService,
    UserService userService,
    ILogger<ChatToolExecutor> log)
{
    public const string GenerateImageTool = "generate_image";

    public static readonly IList<ToolDefinition> Definitions =
    [
        ToolDefinition.DefineFunction(new FunctionDefinition
        {
            Name = GenerateImageTool,
            Description =
                "Generate a picture with GPT Image and send it directly to the user in this chat. " +
                "Only call this when the user explicitly asks to draw/create/generate an image, picture, " +
                "illustration or photo. It costs the user Telegram Stars, so do not call it speculatively " +
                "or more than once per request. Do not use it for OCR or for describing a photo the user " +
                "already sent.",
            Parameters = PropertyDefinition.DefineObject(
                new Dictionary<string, PropertyDefinition>
                {
                    ["prompt"] = PropertyDefinition.DefineString(
                        "Detailed image description, in the same language the user used."),
                    ["size"] = PropertyDefinition.DefineEnum(
                        [GptImageConfig.SizeSquare, GptImageConfig.SizeLandscape, GptImageConfig.SizePortrait],
                        "Aspect ratio: square (1:1), landscape (3:2) or portrait (2:3). Default: square."),
                    ["quality"] = PropertyDefinition.DefineEnum(
                        [GptImageConfig.QualityLow, GptImageConfig.QualityMedium, GptImageConfig.QualityHigh],
                        "Rendering quality/cost tier. Default: medium."),
                },
                required: ["prompt"],
                additionalProperties: false,
                description: null,
                @enum: null),
        }),
    ];

    /// <summary>Tool calls move money — keep them out of groups, same as the other paid flows.</summary>
    public bool IsEnabledFor(UpdateDecorator update) => !update.IsGroupOrChannel;

    public Task<string> ExecuteAsync(ToolCall call, UpdateDecorator update, CancellationToken token)
    {
        if (call.FunctionCall?.Name != GenerateImageTool)
        {
            return Task.FromResult($"error: unknown tool '{call.FunctionCall?.Name}'");
        }

        return ExecuteGenerateImageAsync(call.FunctionCall, update, token);
    }

    private async Task<string> ExecuteGenerateImageAsync(
        FunctionCall call,
        UpdateDecorator update,
        CancellationToken token)
    {
        Dictionary<string, object> args;
        try
        {
            args = call.ParseArguments();
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "Failed to parse {Tool} arguments", GenerateImageTool);
            return "error: could not parse arguments; ask the user to rephrase the request";
        }

        var prompt = args.TryGetValue("prompt", out var promptValue) ? promptValue?.ToString()?.Trim() : null;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return "error: prompt is required";
        }

        if (prompt.Length > GptImageConfig.PromptLimit)
        {
            prompt = prompt[..GptImageConfig.PromptLimit];
        }

        var size = GptImageConfig.ResolveSize(args.TryGetValue("size", out var sizeValue) ? sizeValue?.ToString() : null);
        var quality = GptImageConfig.ResolveQuality(
            args.TryGetValue("quality", out var qualityValue) ? qualityValue?.ToString() : null);
        var starsCost = quality.StarsCost;

        var chatId = update.UserChatKey.ChatId;
        var hold = await userService.TryReserveGptImageAsync(update.UserChatKey.Id, starsCost);
        if (hold is null)
        {
            return $"error: insufficient_balance — the user needs {starsCost} Telegram Stars to generate " +
                   "an image with GPT Image and does not have enough. Politely tell them the cost, in " +
                   "their own language, and that they can top up via /deposit. Do not claim an image was made.";
        }

        try
        {
            var imageBytes = await imageCreatorService.GenerateAsync(prompt, size.Id, quality.Id, chatId, token);

            await using var stream = new MemoryStream(imageBytes);
            await botClient.SendPhoto(
                chatId,
                InputFile.FromStream(stream, "gpt-image-2.png"),
                replyParameters: update.Message?.TelegramMessageId is long mid
                    ? new ReplyParameters { MessageId = (int)mid }
                    : null,
                replyMarkup: TelegramBotUiService.MenuIfPrivate(chatId),
                cancellationToken: token);

            await userService.ConfirmAsync(hold.Id);
            return "success: the image was generated and already sent to the user as a photo in this chat " +
                   $"(quality={quality.Id}, size={size.Id}, cost={starsCost} Stars). Reply briefly — do not " +
                   "restate the visual description in detail, the user can already see the picture.";
        }
        catch (ClientException ex)
        {
            await userService.ReleaseAsync(hold.Id);
            log.LogInformation(ex, "{Tool} call failed with a client error", GenerateImageTool);
            return $"error: {ex.Message}";
        }
        catch (Exception ex)
        {
            await userService.ReleaseAsync(hold.Id);
            log.LogError(ex, "{Tool} call failed", GenerateImageTool);
            return "error: image generation failed unexpectedly; apologize and suggest trying again later.";
        }
    }
}
