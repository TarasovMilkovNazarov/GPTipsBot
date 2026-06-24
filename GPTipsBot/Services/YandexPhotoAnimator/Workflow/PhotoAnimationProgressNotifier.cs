using GPTipsBot.Resources;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class PhotoAnimationProgressNotifier(ITelegramBotClient botClient)
{
    public async Task<int> StartAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var message = await botClient.SendMessage(
            chatId,
            BotResponse.PleaseWaitVideoMsg,
            replyMarkup: TelegramBotUiService.CancelInlineKeyboard,
            cancellationToken: cancellationToken);

        return message.MessageId;
    }

    public async Task UpdateAsync(long chatId, int messageId, string text, CancellationToken cancellationToken = default)
    {
        try
        {
            await botClient.EditMessageText(chatId, messageId, text, cancellationToken: cancellationToken);
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException ex) when (ex.Message.Contains("message is not modified"))
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

    public async Task ReportWaitingAsync(
        long chatId,
        int messageId,
        int remainingSeconds,
        int attempt,
        int maxAttempts,
        CancellationToken cancellationToken = default)
    {
        var text = string.Format(BotResponse.PhotoAnimationWaiting, remainingSeconds, attempt, maxAttempts);
        await UpdateAsync(chatId, messageId, text, cancellationToken);
    }
}
