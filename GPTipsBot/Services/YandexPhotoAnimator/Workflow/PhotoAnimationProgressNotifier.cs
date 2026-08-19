using System.Globalization;
using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class PhotoAnimationProgressNotifier(ITelegramBotClient botClient)
{
    public async Task<int> StartAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var message = await botClient.SendMessageWithMenuAsync(
            chatId,
            BotResponse.PleaseWaitVideoMsg,
            TelegramBotUiService.CancelInlineKeyboard,
            cancellationToken: cancellationToken);

        return message.MessageId;
    }

    public async Task UpdateAsync(long chatId, int messageId, string text, CancellationToken cancellationToken = default)
    {
        try
        {
            await botClient.EditMessageText(chatId, messageId, text, cancellationToken: cancellationToken);
        }
        catch (ApiRequestException ex) when (ex.Message.Contains("message is not modified"))
        {
            // Telegram returns error when message text is unchanged.
        }
    }

    public async Task ReportDownloadingPhotoAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
        => await UpdateAsync(chatId, messageId, BotResponse.PhotoAnimationDownloading, cancellationToken);

    public async Task ReportUploadingImageAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
        => await UpdateAsync(chatId, messageId, BotResponse.PhotoAnimationUploading, cancellationToken);

    public async Task ReportStartingGenerationAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
        => await UpdateAsync(chatId, messageId, BotResponse.PhotoAnimationGenerating, cancellationToken);

    public async Task<int> StartImageAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var message = await botClient.SendMessageWithMenuAsync(
            chatId,
            BotResponse.PleaseWaitImageMsg,
            TelegramBotUiService.CancelInlineKeyboard,
            cancellationToken: cancellationToken);

        return message.MessageId;
    }

    public async Task ReportStartingImageGenerationAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
        => await UpdateAsync(chatId, messageId, BotResponse.PhotoImageGenerating, cancellationToken);

    public async Task ReportImageWaitingAsync(
        long chatId,
        int messageId,
        int remainingSeconds,
        CancellationToken cancellationToken = default)
    {
        var formatted = FormatRemainingTime(remainingSeconds);
        var text = string.Format(BotResponse.PhotoImageWaiting, formatted);
        await UpdateAsync(chatId, messageId, text, cancellationToken);
    }

    public async Task ReportWaitingAsync(
        long chatId,
        int messageId,
        int remainingSeconds,
        CancellationToken cancellationToken = default)
    {
        var formatted = FormatRemainingTime(remainingSeconds);
        var text = string.Format(BotResponse.PhotoAnimationWaiting, formatted);
        await UpdateAsync(chatId, messageId, text, cancellationToken);
    }

    private static string FormatRemainingTime(int remainingSeconds)
    {
        return PhotoAnimationWaitPolicy.FormatRemainingTimeForCulture(
            remainingSeconds,
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }
}
