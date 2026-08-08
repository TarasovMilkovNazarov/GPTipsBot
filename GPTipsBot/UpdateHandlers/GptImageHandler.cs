using System.Diagnostics;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.Cache;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers;

public class GptImageHandler(
    ITelegramBotClient botClient,
    ImageCreatorService imageCreatorService,
    UserService userService,
    IGptImageSessionCache sessionCache,
    ILogger<GptImageHandler> log,
    IAdvertisementClient gramadsAdvertisementClient,
    TelejetAdClient telejetAdClient,
    InMemoryAdvertisementTracker advertisementTracker)
    : BaseMessageHandler
{
    public override async Task HandleAsync(UpdateDecorator update)
    {
        var userId = update.UserChatKey.Id;
        var chatId = update.UserChatKey.ChatId;
        var session = sessionCache.GetOrCreate(userId);

            if (string.IsNullOrWhiteSpace(update.Message?.Text))
            {
                var hint = session.Mode == GptImageMode.Edit
                    ? string.Format(BotResponse.GptImageSendEditPrompt, session.StarsCost)
                    : BotResponse.GptImageSendPrompt;
                await botClient.SendMessage(
                    chatId,
                    hint,
                    replyMarkup: TelegramBotUiService.GetGptImageOptionsKeyboard(session));
                return;
            }

        var prompt = update.Message.Text.Trim();
        if (prompt.Length > GptImageConfig.PromptLimit)
        {
            await botClient.SendMessage(
                chatId,
                string.Format(BotResponse.ImageDescriptionLimitWarning, GptImageConfig.PromptLimit),
                replyMarkup: TelegramBotUiService.GetCancelMarkup(update.IsGroupOrChannel));
            return;
        }

        if (session.Mode == GptImageMode.Edit && string.IsNullOrWhiteSpace(session.ImageFileId))
        {
            await botClient.SendMessage(
                chatId,
                BotResponse.GptImageSendPhotoFirst,
                replyMarkup: TelegramBotUiService.CancelInlineKeyboard);
            return;
        }

        var hold = await userService.TryReserveGptImageAsync(userId, session.StarsCost);
        if (hold is null)
        {
            await botClient.SendMessage(
                chatId,
                string.Format(BotResponse.InsufficientBalance, session.StarsCost),
                replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
            return;
        }

        var confirmed = false;
        var progress = await botClient.SendMessage(chatId, BotResponse.PleaseWaitMsg);

        try
        {
            var sw = Stopwatch.StartNew();
            byte[] imageBytes;

            if (session.Mode == GptImageMode.Edit)
            {
                var photoBase64 = await session.ImageFileId!.GetPhotoAsync(botClient);
                var sourceBytes = Convert.FromBase64String(photoBase64);
                imageBytes = await imageCreatorService.EditAsync(
                    prompt,
                    sourceBytes,
                    "photo.png",
                    session.Size,
                    session.Quality,
                    chatId);
            }
            else
            {
                imageBytes = await imageCreatorService.GenerateAsync(
                    prompt,
                    session.Size,
                    session.Quality,
                    chatId);
            }

            sw.Stop();
            log.LogInformation(
                "GPT Image 2 {Mode} took {Elapsed}s size={Size} quality={Quality}",
                session.Mode,
                sw.Elapsed.TotalSeconds,
                session.Size,
                session.Quality);

            await using var stream = new MemoryStream(imageBytes);
            await botClient.SendPhoto(
                chatId,
                InputFile.FromStream(stream, "gpt-image-2.png"),
                caption: string.Format(
                    BotResponse.GptImageDoneCaption,
                    session.Quality,
                    session.Size,
                    session.StarsCost),
                replyParameters: update.Message.TelegramMessageId is long mid
                    ? new ReplyParameters { MessageId = (int)mid }
                    : null);

            try
            {
                await botClient.DeleteMessage(chatId, progress.MessageId);
            }
            catch
            {
                // ignore delete failures
            }

            await userService.ConfirmAsync(hold.Id);
            confirmed = true;
            sessionCache.Remove(userId);
        }
        catch (ClientException ex)
        {
            log.LogInformation(ex, "GPT Image 2 client error");
            await botClient.SendMessage(chatId, ex.Message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "GPT Image 2 failed");
            await botClient.SendMessage(chatId, BotResponse.SomethingWentWrong);
        }
        finally
        {
            if (!confirmed)
            {
                await userService.ReleaseAsync(hold.Id);
            }
        }

        if (!confirmed || update.IsGroupOrChannel)
        {
            return;
        }

        await gramadsAdvertisementClient.SendPostToChat(chatId);
        await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
        await advertisementTracker.TrySendAdvertisement(userId);
    }
}
