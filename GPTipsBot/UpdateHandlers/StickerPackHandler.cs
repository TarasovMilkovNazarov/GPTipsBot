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
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers;

public class StickerPackHandler(
    ITelegramBotClient botClient,
    StickerPackService stickerPackService,
    UserService userService,
    IStickerPackSessionCache sessionCache,
    ILogger<StickerPackHandler> log)
    : BaseMessageHandler
{
    public override async Task HandleAsync(UpdateDecorator update)
    {
        var userId = update.UserChatKey.Id;
        var chatId = update.UserChatKey.ChatId;
        var session = sessionCache.GetOrCreate(userId);
        var command = update.Command?.Command;

        if (session.Busy)
        {
            await botClient.SendUserReplyAsync(update, BotResponse.PleaseWaitMsg);
            return;
        }

        if (command == BotMenu.StickersHeroOkCommand)
        {
            await GeneratePackAsync(update, session);
            return;
        }

        if (command == BotMenu.StickersHeroRedoCommand)
        {
            await GenerateHeroAsync(update, session);
            return;
        }

        if (command == BotMenu.StickersPublishCommand)
        {
            await PublishAsync(update, session);
            return;
        }

        if (update.FileId != null)
        {
            session.SourceFileId = update.FileId;
        }

        if (!update.IsCommand && !string.IsNullOrWhiteSpace(update.Message?.Text))
        {
            var text = update.Message.Text.Trim();
            if (!text.StartsWith('/'))
            {
                var route = NaturalLanguageToolRouter.TryMatch(text, update.FileId != null);
                if (route.Intent == MediaToolIntent.StickerPack)
                {
                    if (!string.IsNullOrWhiteSpace(route.Prompt))
                    {
                        session.Description = route.Prompt;
                    }
                }
                else
                {
                    session.Description = text;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(session.SourceFileId) && string.IsNullOrWhiteSpace(session.Description))
        {
            await botClient.SendUserReplyAsync(
                update,
                string.Format(BotResponse.StickerPackIntro, StickerPackConfig.HeroStars,
                    StickerPackConfig.PackRemainderStars),
                TelegramBotUiService.BackToImagesMenuInlineKeyboard);
            return;
        }

        sessionCache.Set(userId, session);
        await GenerateHeroAsync(update, session);
    }

    private async Task GenerateHeroAsync(UpdateDecorator update, StickerPackSession session)
    {
        if (string.IsNullOrWhiteSpace(session.SourceFileId) && string.IsNullOrWhiteSpace(session.Description))
        {
            await botClient.SendUserReplyAsync(
                update,
                string.Format(BotResponse.StickerPackIntro, StickerPackConfig.HeroStars,
                    StickerPackConfig.PackRemainderStars),
                TelegramBotUiService.BackToImagesMenuInlineKeyboard);
            return;
        }

        var hold = await userService.TryReserveGptImageAsync(update.UserChatKey.Id, StickerPackConfig.HeroStars);
        if (hold is null)
        {
            await botClient.SendMessageWithMenuAsync(
                update.UserChatKey.ChatId,
                string.Format(BotResponse.InsufficientBalance, StickerPackConfig.HeroStars),
                TelegramBotUiService.DepositInlineKeyboard,
                update.IsGroupOrChannel);
            return;
        }

        session.Busy = true;
        sessionCache.Set(update.UserChatKey.Id, session);

        var confirmed = false;
        var threadId = update.Message?.MessageThreadId is long tid ? (int?)tid : null;
        var progress = await botClient.SendMessage(
            update.UserChatKey.ChatId,
            string.Format(BotResponse.StickerPackProgress, 1, StickerPackConfig.EmotionCount),
            messageThreadId: threadId);

        try
        {
            string? photoBase64 = null;
            if (!string.IsNullOrWhiteSpace(session.SourceFileId))
            {
                photoBase64 = await session.SourceFileId.GetPhotoAsync(botClient);
            }

            var sw = Stopwatch.StartNew();
            var hero = await stickerPackService.GenerateHeroAsync(
                photoBase64,
                session.Description,
                update.UserChatKey.ChatId,
                CancellationToken.None);
            sw.Stop();
            log.LogInformation("Sticker hero took {Elapsed}s for user {UserId}", sw.Elapsed.TotalSeconds,
                update.UserChatKey.Id);

            session.HeroPng = hero.Png;
            session.Stickers = [hero];
            session.Step = StickerPackStep.HeroReady;

            await using var stream = new MemoryStream(hero.Png);
            await botClient.SendPhoto(
                update.UserChatKey.ChatId,
                InputFile.FromStream(stream, "sticker-hero.png"),
                caption: string.Format(BotResponse.StickerPackHeroCaption, StickerPackConfig.HeroStars,
                    StickerPackConfig.PackRemainderStars),
                messageThreadId: threadId,
                replyMarkup: TelegramBotUiService.GetStickerHeroKeyboard(),
                replyParameters: update.Message?.TelegramMessageId is long mid
                    ? new ReplyParameters { MessageId = (int)mid }
                    : null);

            await userService.ConfirmAsync(hold.Id);
            confirmed = true;
        }
        catch (ClientException ex)
        {
            log.LogInformation(ex, "Sticker hero client error");
            await botClient.SendMessageWithMenuAsync(
                update.UserChatKey.ChatId, ex.Message, isGroupOrChannel: update.IsGroupOrChannel,
                messageThreadId: threadId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Sticker hero failed");
            await botClient.SendMessageWithMenuAsync(
                update.UserChatKey.ChatId, BotResponse.SomethingWentWrong,
                isGroupOrChannel: update.IsGroupOrChannel, messageThreadId: threadId);
        }
        finally
        {
            session.Busy = false;
            sessionCache.Set(update.UserChatKey.Id, session);
            if (!confirmed)
            {
                await userService.ReleaseAsync(hold.Id);
            }

            try
            {
                await botClient.DeleteMessage(update.UserChatKey.ChatId, progress.MessageId);
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Failed to delete sticker hero progress {MessageId}", progress.MessageId);
            }
        }
    }

    private async Task GeneratePackAsync(UpdateDecorator update, StickerPackSession session)
    {
        if (session.HeroPng == null || session.Step == StickerPackStep.AwaitingSource)
        {
            await botClient.SendUserReplyAsync(
                update,
                string.Format(BotResponse.StickerPackIntro, StickerPackConfig.HeroStars,
                    StickerPackConfig.PackRemainderStars),
                TelegramBotUiService.BackToImagesMenuInlineKeyboard);
            return;
        }

        var hold = await userService.TryReserveGptImageAsync(
            update.UserChatKey.Id, StickerPackConfig.PackRemainderStars);
        if (hold is null)
        {
            await botClient.SendMessageWithMenuAsync(
                update.UserChatKey.ChatId,
                string.Format(BotResponse.InsufficientBalance, StickerPackConfig.PackRemainderStars),
                TelegramBotUiService.DepositInlineKeyboard,
                update.IsGroupOrChannel);
            return;
        }

        session.Busy = true;
        sessionCache.Set(update.UserChatKey.Id, session);

        var confirmed = false;
        var threadId = update.Message?.MessageThreadId is long tid ? (int?)tid : null;
        var progress = await botClient.SendMessage(
            update.UserChatKey.ChatId,
            string.Format(BotResponse.StickerPackProgress, 1, StickerPackConfig.EmotionCount),
            messageThreadId: threadId);

        try
        {
            var progressMessageId = progress.Id;
            var progressReporter = new Progress<int>(drawn =>
            {
                _ = SafeEditProgressAsync(
                    update.UserChatKey.ChatId,
                    progressMessageId,
                    Math.Min(drawn, StickerPackConfig.EmotionCount));
            });

            var sw = Stopwatch.StartNew();
            var variants = await stickerPackService.GenerateVariantsAsync(
                session.HeroPng,
                progressReporter,
                update.UserChatKey.ChatId,
                CancellationToken.None);
            sw.Stop();
            log.LogInformation(
                "Sticker pack variants {Count}/{Expected} took {Elapsed}s for user {UserId}",
                variants.Count,
                StickerPackConfig.EmotionCount - 1,
                sw.Elapsed.TotalSeconds,
                update.UserChatKey.Id);

            var hero = session.Stickers.FirstOrDefault() ?? new StickerDraft
            {
                Emoji = StickerPackConfig.Emotions[0].Emoji,
                Keyword = StickerPackConfig.Emotions[0].Keyword,
                Png = session.HeroPng,
            };
            session.Stickers = [hero, .. variants];
            session.Step = StickerPackStep.PackReady;

            if (session.Stickers.Count < StickerPackConfig.MinStickersToPublish)
            {
                await botClient.SendUserReplyAsync(
                    update,
                    BotResponse.StickerPackPartialFail,
                    TelegramBotUiService.GetStickerHeroKeyboard());
                return;
            }

            await SendPreviewAlbumAsync(update, session.Stickers, threadId);
            await botClient.SendUserReplyAsync(
                update,
                string.Format(BotResponse.StickerPackReady, session.Stickers.Count,
                    StickerPackConfig.PackRemainderStars),
                TelegramBotUiService.GetStickerPublishKeyboard());

            await userService.ConfirmAsync(hold.Id);
            confirmed = true;
        }
        catch (ClientException ex)
        {
            log.LogInformation(ex, "Sticker pack client error");
            await botClient.SendMessageWithMenuAsync(
                update.UserChatKey.ChatId, ex.Message, isGroupOrChannel: update.IsGroupOrChannel,
                messageThreadId: threadId);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Sticker pack generation failed");
            await botClient.SendMessageWithMenuAsync(
                update.UserChatKey.ChatId, BotResponse.SomethingWentWrong,
                isGroupOrChannel: update.IsGroupOrChannel, messageThreadId: threadId);
        }
        finally
        {
            session.Busy = false;
            sessionCache.Set(update.UserChatKey.Id, session);
            if (!confirmed)
            {
                await userService.ReleaseAsync(hold.Id);
            }

            try
            {
                await botClient.DeleteMessage(update.UserChatKey.ChatId, progress.MessageId);
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Failed to delete sticker pack progress {MessageId}", progress.MessageId);
            }
        }
    }

    private async Task PublishAsync(UpdateDecorator update, StickerPackSession session)
    {
        if (session.Step != StickerPackStep.PackReady ||
            session.Stickers.Count < StickerPackConfig.MinStickersToPublish)
        {
            await botClient.SendUserReplyAsync(
                update,
                BotResponse.StickerPackNeedPreview,
                TelegramBotUiService.BackToImagesMenuInlineKeyboard);
            return;
        }

        session.Busy = true;
        sessionCache.Set(update.UserChatKey.Id, session);

        var threadId = update.Message?.MessageThreadId is long tid ? (int?)tid : null;
        var progress = await botClient.SendMessage(
            update.UserChatKey.ChatId,
            BotResponse.PleaseWaitMsg,
            messageThreadId: threadId);

        try
        {
            var title = string.IsNullOrWhiteSpace(session.Description)
                ? BotResponse.StickerPackDefaultTitle
                : session.Description.Trim();
            var url = await stickerPackService.PublishAsync(
                update.TelegramUserId,
                title,
                session.Stickers,
                CancellationToken.None);

            await botClient.SendUserReplyAsync(
                update,
                string.Format(BotResponse.StickerPackPublished, url),
                TelegramBotUiService.MenuIfPrivate(update.UserChatKey.ChatId));

            sessionCache.Remove(update.UserChatKey.Id);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Sticker pack publish failed");
            session.Busy = false;
            sessionCache.Set(update.UserChatKey.Id, session);
            await botClient.SendMessageWithMenuAsync(
                update.UserChatKey.ChatId, BotResponse.StickerPackPublishFail,
                TelegramBotUiService.GetStickerPublishKeyboard(),
                update.IsGroupOrChannel,
                messageThreadId: threadId);
        }
        finally
        {
            try
            {
                await botClient.DeleteMessage(update.UserChatKey.ChatId, progress.MessageId);
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Failed to delete sticker publish progress {MessageId}", progress.MessageId);
            }
        }
    }

    private async Task SendPreviewAlbumAsync(
        UpdateDecorator update,
        IReadOnlyList<StickerDraft> stickers,
        int? threadId)
    {
        var streams = new List<MemoryStream>(stickers.Count);
        try
        {
            var media = stickers.Select((sticker, i) =>
            {
                var stream = new MemoryStream(sticker.Png);
                streams.Add(stream);
                var photo = new InputMediaPhoto(InputFile.FromStream(stream, $"sticker{i}.png"))
                {
                    Caption = i == 0
                        ? string.Format(BotResponse.StickerPackAlbumCaption, stickers.Count)
                        : null,
                };
                return (IAlbumInputMedia)photo;
            }).ToArray();

            await botClient.SendMediaGroup(
                update.UserChatKey.ChatId,
                media,
                messageThreadId: threadId);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private async Task SafeEditProgressAsync(long chatId, int messageId, int current)
    {
        try
        {
            await botClient.EditMessageText(
                chatId,
                messageId,
                string.Format(BotResponse.StickerPackProgress, Math.Min(current, StickerPackConfig.EmotionCount),
                    StickerPackConfig.EmotionCount));
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Failed to edit sticker progress {MessageId}", messageId);
        }
    }
}
