using System.Diagnostics;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.Cache;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers;

/// <summary>
/// Virtual try-on: puts the clothing item from the incoming photo on the person
/// from the photo saved in <see cref="ITryOnSessionCache"/> (GPT Image 2 edit with two input images).
/// </summary>
public class TryOnHandler(
    ITelegramBotClient botClient,
    ImageCreatorService imageCreatorService,
    UserService userService,
    ITryOnSessionCache sessionCache,
    ILogger<TryOnHandler> log)
    : BaseMessageHandler
{
    public override async Task HandleAsync(UpdateDecorator update)
    {
        var userId = update.UserChatKey.Id;
        var chatId = update.UserChatKey.ChatId;
        var session = sessionCache.GetOrCreate(userId);
        var clothesFileId = update.FileId;

        if (string.IsNullOrWhiteSpace(session.PersonFileId) || clothesFileId == null)
        {
            await botClient.SendUserReplyAsync(update, BotResponse.TryOnSendClothesPhoto,
                TelegramBotUiService.TryOnInlineKeyboard);
            return;
        }

        var hold = await userService.TryReserveTryOnAsync(userId);
        if (hold is null)
        {
            await botClient.SendMessageWithMenuAsync(
                chatId,
                string.Format(BotResponse.InsufficientBalance, PaymentConfig.TryOn),
                TelegramBotUiService.DepositInlineKeyboard,
                update.IsGroupOrChannel);
            return;
        }

        var confirmed = false;
        var threadId = update.Message?.MessageThreadId is long tid ? (int?)tid : null;
        var progress = await botClient.SendMessage(
            chatId,
            BotResponse.PleaseWaitMsg,
            messageThreadId: threadId);

        try
        {
            var sw = Stopwatch.StartNew();

            var personBytes = Convert.FromBase64String(await session.PersonFileId.GetPhotoAsync(botClient));
            var clothesBytes = Convert.FromBase64String(await clothesFileId.GetPhotoAsync(botClient));

            var imageBytes = await imageCreatorService.EditAsync(
                BuildPrompt(update.Message?.Text),
                personBytes,
                "person.jpg",
                GptImageConfig.SizePortrait,
                GptImageConfig.QualityMedium,
                chatId,
                additionalImages: [new ImageEditInput(clothesBytes, "clothes.jpg")]);

            sw.Stop();
            log.LogInformation("Try-on took {Elapsed}s", sw.Elapsed.TotalSeconds);

            await using var stream = new MemoryStream(imageBytes);
            await botClient.SendPhoto(
                chatId,
                InputFile.FromStream(stream, "try-on.png"),
                caption: string.Format(BotResponse.TryOnDoneCaption, PaymentConfig.TryOn),
                messageThreadId: threadId,
                replyMarkup: TelegramBotUiService.TryOnResultInlineKeyboard,
                replyParameters: update.Message?.TelegramMessageId is long mid
                    ? new ReplyParameters { MessageId = (int)mid }
                    : null);

            await userService.ConfirmAsync(hold.Id);
            confirmed = true;
        }
        catch (ClientException ex)
        {
            log.LogInformation(ex, "Try-on client error");
            await botClient.SendMessageWithMenuAsync(
                chatId, ex.Message, isGroupOrChannel: update.IsGroupOrChannel, messageThreadId: threadId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Try-on failed");
            await botClient.SendMessageWithMenuAsync(
                chatId, BotResponse.SomethingWentWrong, isGroupOrChannel: update.IsGroupOrChannel, messageThreadId: threadId);
        }
        finally
        {
            if (!confirmed)
            {
                await userService.ReleaseAsync(hold.Id);
            }

            try
            {
                await botClient.DeleteMessage(chatId, progress.MessageId);
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Failed to delete try-on progress message {MessageId}", progress.MessageId);
            }
        }
    }

    public static string BuildPrompt(string? userWishes)
    {
        var prompt = "Virtual clothes try-on. The first image is a photo of a person. The second image shows a clothing item " +
            "(a product photo or a screenshot from an online store). Generate a photorealistic full-body photo of exactly the same " +
            "person from the first image wearing the clothing item from the second image. " +
            "Preserve the person's face, identity, body shape, skin tone, hair, pose and background. " +
            "Replace only the garments that the new item would replace and keep the rest of the outfit. " +
            "Reproduce the item's color, fabric, texture, pattern, logos and cut faithfully, with a realistic fit, natural folds, " +
            "lighting and shadows. Ignore any text, prices, interface elements or other people on the clothing image. " +
            "Do not add text or watermarks.";

        if (!string.IsNullOrWhiteSpace(userWishes))
        {
            prompt += $" Additional wishes from the person: {userWishes.Trim().Truncate(GptImageConfig.PromptLimit / 2)}";
        }

        return prompt;
    }
}
