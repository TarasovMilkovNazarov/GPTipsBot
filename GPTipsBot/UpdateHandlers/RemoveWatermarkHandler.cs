using System.Diagnostics;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.VseGpt;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers;

public class RemoveWatermarkHandler(
    ITelegramBotClient botClient,
    VseGptImageClient vseGptImageClient,
    UserService userService,
    ILogger<RemoveWatermarkHandler> log)
    : BaseMessageHandler
{
    public override async Task HandleAsync(UpdateDecorator update)
    {
        var userId = update.UserChatKey.Id;
        var chatId = update.UserChatKey.ChatId;

        if (update.FileId == null)
        {
            await botClient.SendUserReplyAsync(
                update,
                BotResponse.RemoveWatermarkSendPhoto,
                TelegramBotUiService.CancelInlineKeyboard);
            return;
        }

        var hold = await userService.TryReserveWatermarkRemovalAsync(userId);
        if (hold is null)
        {
            await botClient.SendMessage(
                chatId,
                string.Format(BotResponse.InsufficientBalance, PaymentConfig.WatermarkRemoval),
                replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
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
            var photoBase64 = await update.GetPhotoAsync(botClient);

            var sw = Stopwatch.StartNew();
            var imageBytes = await vseGptImageClient.EditAsync(
                WatermarkRemovalConfig.ModelId,
                WatermarkRemovalConfig.Prompt,
                photoBase64,
                chatId);
            sw.Stop();

            log.LogInformation("Watermark removal took {Elapsed}s for user {UserId}",
                sw.Elapsed.TotalSeconds, userId);

            await using var stream = new MemoryStream(imageBytes);
            await botClient.SendPhoto(
                chatId,
                InputFile.FromStream(stream, "no-watermark.jpg"),
                caption: string.Format(
                    BotResponse.RemoveWatermarkDoneCaption,
                    PaymentConfig.WatermarkRemoval),
                messageThreadId: threadId,
                replyParameters: update.Message?.TelegramMessageId is long mid
                    ? new ReplyParameters { MessageId = (int)mid }
                    : null);

            await userService.ConfirmAsync(hold.Id);
            confirmed = true;

            // The command stays active, so say so explicitly: the next photo is charged again.
            await botClient.SendMessage(
                chatId,
                string.Format(BotResponse.RemoveWatermarkModeActive, PaymentConfig.WatermarkRemoval),
                messageThreadId: threadId,
                replyMarkup: TelegramBotUiService.CancelInlineKeyboard);
        }
        catch (ClientException ex)
        {
            log.LogInformation(ex, "Watermark removal client error");
            await botClient.SendMessage(chatId, ex.Message, messageThreadId: threadId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Watermark removal failed");
            await botClient.SendMessage(chatId, BotResponse.SomethingWentWrong, messageThreadId: threadId);
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
                log.LogDebug(ex, "Failed to delete progress message {MessageId}", progress.MessageId);
            }
        }
    }
}
