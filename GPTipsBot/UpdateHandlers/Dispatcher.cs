using GPTipsBot.Dtos;
using GPTipsBot.Mapper;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using GPTipsBot.Config;
using GPTipsBot.Enums;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Localization;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services.Cache;
using GPTipsBot.Services.Broadcast;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Services.YandexPhotoAnimator.Workflow;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class Dispatcher(
        ITelegramBotClient botClient,
        RecoveryNotificationHandler recoveryNotificationHandler,
        ImageTextRecognitionHandler imageTextRecognitionHandler,
        PromptFromImageHandler promptFromImageHandler,
        ImageGeneratorHandler imageGeneratorHandler,
        GptImageHandler gptImageHandler,
        StickerPackHandler stickerPackHandler,
        RemoveWatermarkHandler removeWatermarkHandler,
        CommandHandler commandHandler,
        ChatGptHandler chatGptHandler,
        AdminCommandHandler adminCommandHandler,
        InlineQueryHandler inlineQueryHandler,
        ILogger<Dispatcher> logger,
        UserService userService,
        UserCommandRepository userCommandRepository,
        MoneyService moneyService,
        BotSettingsRepository botSettingsRepository,
        InvoiceRepository invoiceRepository,
        IGpt gptService,
        IImageCache imageCache,
        IAliceImageSessionCache aliceImageSessionCache,
        IVisionImageCache visionImageCache,
        IGptImageSessionCache gptImageSessionCache,
        IStickerPackSessionCache stickerPackSessionCache,
        MessageRepository messageRepository,
        PhotoAnimationProgressNotifier photoAnimationProgressNotifier,
        IJobService jobService,
        MediaIntentClassifier mediaIntentClassifier,
        BroadcastDraftStore broadcastDraftStore)
        : BaseMessageHandler
    {
        public static readonly ConcurrentDictionary<UserChatKey, UserStateDto> UserState = new ();

        public override async Task HandleAsync(UpdateDecorator update)
        {
            await ResolveInternalUserAsync(update);

            var userKey = update.UserChatKey;

            if (!UserState.ContainsKey(userKey))
            {
                UserState.TryAdd(userKey, new UserStateDto());
            }

            var newUser = UserMapper.Map(update.User);
            try
            {
                await userService.CreateUpdateUser(newUser);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Couldn't create user with telegramId {userId} in database",
                    update.TelegramUserId);
            }

            var settings = botSettingsRepository.Get(userKey.Id);
            string language;
            if (settings == null)
            {
                language = LocalizationManager.NormalizeLanguage(update.Language);
                botSettingsRepository.Create(userKey.Id, language);
            }
            else
            {
                language = settings.Language;
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(language);

            if (!string.IsNullOrEmpty(update.FileId) && !update.IsCommand)
            {
                visionImageCache.Remember(update.UserChatKey, update.FileId);
            }

            if (update.IsInline)
            {
                await inlineQueryHandler.HandleAsync(update);
                return;
            }

            if (update.CallbackQuery != null &&
                InlineQueryHandler.IsInlineCallback(update.CallbackQuery.Data))
            {
                await inlineQueryHandler.HandleCallbackAsync(update);
                return;
            }

            var lastCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);

            if (update.IsGroupOrChannel && !update.IsAddressedToBot && !IsGroupFollowUp(lastCommand))
            {
                if (!string.IsNullOrWhiteSpace(update.Message?.Text))
                {
                    update.Message.ContextBound = false;
                    await messageRepository.AddAsync(update.Message);
                }

                return;
            }

            if (update.PreCheckoutQuery != null)
            {
                if (moneyService.TryValidatePreCheckout(update.PreCheckoutQuery, out var errorMessage))
                {
                    await botClient.AnswerPreCheckoutQuery(
                        preCheckoutQueryId: update.PreCheckoutQuery.Id);
                }
                else
                {
                    await botClient.AnswerPreCheckoutQuery(
                        preCheckoutQueryId: update.PreCheckoutQuery.Id,
                        errorMessage: errorMessage ?? "Invalid invoice");
                }

                return;
            }

            if (update.CallbackQuery != null &&
                PaymentCallbacks.TryParseCheck(update.CallbackQuery.Data, out var checkInvoiceId))
            {
                var syncResult = await moneyService.SyncYooKassaInvoiceAsync(
                    checkInvoiceId,
                    update.UserChatKey.Id,
                    CancellationToken.None);

                if (syncResult == PaymentConfirmResult.DepositCredited)
                {
                    await botClient.AnswerCallbackQuery(
                        update.CallbackQuery.Id,
                        BotResponse.YooKassaPaymentCheckOk);
                }
                else
                {
                    await botClient.AnswerCallbackQuery(
                        update.CallbackQuery.Id,
                        BotResponse.YooKassaPaymentCheckPending,
                        showAlert: true);
                }

                return;
            }

            if (update.CallbackQuery != null &&
                PaymentCallbacks.TryParsePackage(update.CallbackQuery.Data, out var packageStars))
            {
                await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                await botClient.SendUserReplyAsync(
                    update,
                    string.Format(BotResponse.ChoosePaymentMethod, packageStars,
                        MoneyService.FormatRubAmount(packageStars)),
                    moneyService.BuildPaymentMethodKeyboard(packageStars));
                return;
            }

            if (update.CallbackQuery != null &&
                PaymentCallbacks.TryParse(update.CallbackQuery.Data, out var paymentProvider, out var payStarsCount))
            {
                await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);

                if (paymentProvider == PaymentCallbacks.StarsPrefix)
                {
                    await moneyService.SendInvoice(
                        update.UserChatKey.Id,
                        update.TelegramUserId,
                        payStarsCount);
                }
                else if (paymentProvider == PaymentCallbacks.YooKassaPrefix)
                {
                    await moneyService.CreateYooKassaPaymentAsync(
                        update.UserChatKey.Id,
                        update.TelegramUserId,
                        payStarsCount,
                        CancellationToken.None);
                }

                await userCommandRepository.AddAsync(update.UserChatKey, CommandType.CancelPreviousCommand);
                return;
            }

            if (update.Message?.SuccessfulPayment != null)
            {
                var confirmResult = await moneyService.ConfirmSuccessfulPaymentAsync(
                    update.Message.SuccessfulPayment,
                    update.UserChatKey.Id,
                    CancellationToken.None);

                if (confirmResult == PaymentConfirmResult.DonateConfirmed)
                {
                    await botClient.SendUserReplyAsync(update, BotResponse.DonateText);
                }
                else if (confirmResult == PaymentConfirmResult.DepositCredited)
                {
                    var profile = await userService.GetUserProfile(update.UserChatKey.Id);
                    var reply = string.Format(BotResponse.ProfileResponse, profile.FirstName,
                        profile.LastName, profile.Stars, profile.GptRequests, profile.Images, profile.ImageTexts,
                        profile.PhotoAnimations, profile.Summaries, profile.GptModelDisplayName,
                        profile.CombinePhotos, profile.ChangePhotos);
                    var replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

                    await botClient.SendUserReplyAsync(update, reply, replyMarkup);
                }

                await userCommandRepository.AddAsync(update.UserChatKey, CommandType.CancelPreviousCommand);

                return;
            }

            if (update.IsAdminCommand() ||
                (update.UserChatKey.IsAdmin() && BroadcastCallbacks.IsMatch(update.CallbackQuery?.Data)) ||
                (update.UserChatKey.IsAdmin() &&
                 broadcastDraftStore.IsAwaitingText(update.UserChatKey.TelegramUserId ?? update.UserChatKey.Id) &&
                 !update.IsCommand &&
                 !string.IsNullOrEmpty(update.Message?.Text)))
            {
                SetNextHandler(adminCommandHandler);
            }
            else if (update.IsCommand &&
                     update.Command?.Type is CommandType.Admin or CommandType.Broadcast)
            {
                return;
            }
            else if (update.IsExpired() && update.CallbackQuery == null)
            {
                SetNextHandler(recoveryNotificationHandler);
            }
            else if (update.CallbackQuery != null || update.IsCommand)
            {
                SetNextHandler(commandHandler);
            }
            else if (!string.IsNullOrEmpty(update.Message?.Text) &&
                     lastCommand?.Type is CommandType.Image or CommandType.ImageSquare or CommandType.ImageRectangle)
            {
                SetNextHandler(imageGeneratorHandler);
            }
            else if (lastCommand?.Type is CommandType.GptImage or CommandType.EditImage)
            {
                var session = gptImageSessionCache.GetOrCreate(userKey.Id);
                if (lastCommand.Type == CommandType.EditImage)
                {
                    session.Mode = GptImageMode.Edit;
                }

                if (session.Mode == GptImageMode.Edit)
                {
                    var imageId = update.FileId;
                    if (imageId == null && string.IsNullOrWhiteSpace(session.ImageFileId))
                    {
                        await botClient.SendUserReplyAsync(update, BotResponse.GptImageSendPhotoFirst,
                            TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                        return;
                    }

                    if (imageId != null)
                    {
                        session.ImageFileId = imageId;
                        gptImageSessionCache.Set(userKey.Id, session);

                        if (string.IsNullOrWhiteSpace(update.Message?.Text))
                        {
                            await botClient.SendUserReplyAsync(
                                update,
                                string.Format(BotResponse.GptImageSendEditPrompt, session.StarsCost),
                                TelegramBotUiService.GetGptImageOptionsKeyboard(session));
                            return;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(update.Message?.Text))
                    {
                        await botClient.SendUserReplyAsync(
                            update,
                            string.Format(BotResponse.GptImageSendEditPrompt, session.StarsCost),
                            TelegramBotUiService.GetGptImageOptionsKeyboard(session));
                        return;
                    }

                    gptImageSessionCache.Set(userKey.Id, session);
                    SetNextHandler(gptImageHandler);
                }
                else if (!string.IsNullOrEmpty(update.Message?.Text))
                {
                    SetNextHandler(gptImageHandler);
                }
                else
                {
                    return;
                }
            }
            else if (lastCommand?.Type == CommandType.RemoveWatermark && !update.IsGroupOrChannel)
            {
                SetNextHandler(removeWatermarkHandler);
            }
            else if (lastCommand?.Type == CommandType.StickerPack && !update.IsGroupOrChannel)
            {
                SetNextHandler(stickerPackHandler);
            }
            else if (update.IsGroupOrChannel &&
                     lastCommand?.Type is CommandType.TextRecognition
                         or CommandType.Deposit
                         or CommandType.Donate
                         or CommandType.AnimatePhoto
                         or CommandType.CombinePhoto
                         or CommandType.ChangePhoto
                         or CommandType.GptImage
                         or CommandType.RemoveWatermark
                         or CommandType.StickerPack)
            {
                await botClient.SendMessage(userKey.ChatId, BotResponse.GroupCommandNotAvailable);
                return;
            }
            else if (lastCommand?.Type == CommandType.TextRecognition)
            {
                SetNextHandler(imageTextRecognitionHandler);
            }
            else if (lastCommand?.Type == CommandType.PromptFromImage)
            {
                SetNextHandler(promptFromImageHandler);
            }
            else if (lastCommand?.Type == CommandType.Deposit)
            {
                if (!int.TryParse(update.Message?.Text, out var starsCount) ||
                    starsCount < PaymentConfig.MinRechargeStars ||
                    MoneyService.ToKopecks(starsCount) < PaymentConfig.MinRechargeRub * 100L)
                {
                    throw new ClientCanceledException(update.UserChatKey.ChatId,
                        string.Format(BotResponse.InvalidDepositAmountResponse,
                            PaymentConfig.MinRechargeRub, PaymentConfig.MinRechargeStars));
                }

                await botClient.SendUserReplyAsync(
                    update,
                    string.Format(BotResponse.ChoosePaymentMethod, starsCount,
                        MoneyService.FormatRubAmount(starsCount)),
                    moneyService.BuildPaymentMethodKeyboard(starsCount));

                return;
            }
            else if (lastCommand?.Type == CommandType.Donate)
            {
                if (!int.TryParse(update.Message?.Text, out var starsCount) ||
                    starsCount <= 0)
                {
                    throw new ClientCanceledException(update.UserChatKey.ChatId, BotResponse.StarsDonationHint);
                }

                await moneyService.SendDonateInvoice(
                    update.UserChatKey.Id,
                    update.TelegramUserId,
                    starsCount);

                return;
            }
            else if (lastCommand?.Type == CommandType.AnimatePhoto)
            {
                var imageId = update.FileId;
                if (update.FileId == null && imageCache.TryGet(update.UserChatKey.ChatId, out imageId) == false)
                {
                    await botClient.SendAnimatePhotoInstructionsAsync(
                        update.UserChatKey.ChatId,
                        TelegramBotUiService.CancelInlineKeyboard,
                        update.Message?.MessageThreadId is long tid ? (int)tid : null);

                    return;
                }

                if (string.IsNullOrWhiteSpace(update.Message?.Text))
                {
                    imageCache.Set(update.UserChatKey.ChatId, imageId!);
                    await botClient.SendUserReplyAsync(update, BotResponse.SendAnimatePrompt,
                        TelegramBotUiService.CancelInlineKeyboard);
                    return;
                }

                var hold = await userService.TryReserveAnimationAsync(userKey.Id);
                if (hold is null)
                {
                    var nextExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                    await botClient.SendOutOfFreeRequestsMessageAsync(update.ReplyChatId, nextExec);
                    return;
                }

                try
                {
                    var progressMessageId = await photoAnimationProgressNotifier.StartAsync(update.ReplyChatId);

                    await gptService.StartAnimatePhoto(
                        update.Message.Text,
                        imageId!,
                        update.ReplyChatId,
                        userKey.Id,
                        progressMessageId,
                        hold.Id);
                }
                catch
                {
                    await userService.ReleaseAsync(hold.Id);
                    throw;
                }

                imageCache.Remove(userKey.ChatId);
                return;
            }
            else if (lastCommand?.Type == CommandType.ChangePhoto)
            {
                var imageId = update.FileId;
                if (update.FileId == null && imageCache.TryGet(update.UserChatKey.ChatId, out imageId) == false)
                {
                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.SendPhotoToChange,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return;
                }

                if (string.IsNullOrWhiteSpace(update.Message?.Text))
                {
                    imageCache.Set(update.UserChatKey.ChatId, imageId!);
                    await botClient.SendUserReplyAsync(update, BotResponse.SendChangePrompt,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return;
                }

                if (!await TryStartAliceImageAsync(
                        update,
                        userKey,
                        AliceImageKind.Editing,
                        update.Message.Text,
                        imageId!,
                        imageFileId2: null))
                {
                    return;
                }

                imageCache.Remove(userKey.ChatId);
                return;
            }
            else if (lastCommand?.Type == CommandType.CombinePhoto)
            {
                var session = aliceImageSessionCache.GetOrCreate(userKey.ChatId);
                if (string.IsNullOrEmpty(session.FirstFileId))
                {
                    if (update.FileId == null)
                    {
                        await botClient.SendUserReplyAsync(
                            update,
                            BotResponse.SendFirstPhotoToCombine,
                            TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                        return;
                    }

                    session.FirstFileId = update.FileId;
                    aliceImageSessionCache.Set(userKey.ChatId, session);
                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.SendSecondPhotoToCombine,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return;
                }

                if (string.IsNullOrEmpty(session.SecondFileId))
                {
                    if (update.FileId == null)
                    {
                        await botClient.SendUserReplyAsync(
                            update,
                            BotResponse.SendSecondPhotoToCombine,
                            TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                        return;
                    }

                    session.SecondFileId = update.FileId;
                    aliceImageSessionCache.Set(userKey.ChatId, session);

                    if (string.IsNullOrWhiteSpace(update.Message?.Text))
                    {
                        await botClient.SendUserReplyAsync(
                            update,
                            BotResponse.SendCombinePrompt,
                            TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(update.Message?.Text))
                {
                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.SendCombinePrompt,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return;
                }

                if (!await TryStartAliceImageAsync(
                        update,
                        userKey,
                        AliceImageKind.Combining,
                        update.Message.Text,
                        session.FirstFileId,
                        session.SecondFileId))
                {
                    return;
                }

                aliceImageSessionCache.Remove(userKey.ChatId);
                return;
            }
            else if (TryGetMediaToolRoute(update, out var mediaRoute))
            {
                if (await TryRouteMediaToolAsync(update, mediaRoute))
                {
                    return;
                }

                SetNextHandler(chatGptHandler);
            }
            else if (string.IsNullOrWhiteSpace(update.Message?.Text))
            {
                return;
            }
            else if (await TryLlmFallbackRouteAsync(update))
            {
                return;
            }
            else
            {
                SetNextHandler(chatGptHandler);
            }

            await base.HandleAsync(update);
        }

        private static bool TryGetMediaToolRoute(UpdateDecorator update, out MediaToolRoute route)
        {
            route = MediaToolRoute.None;
            if (update.Message == null)
            {
                return false;
            }

            var hasPhoto = update.FileId != null;
            var hasText = !string.IsNullOrWhiteSpace(update.Message.Text);
            if (!hasPhoto && !hasText)
            {
                return false;
            }

            route = NaturalLanguageToolRouter.TryMatch(update.Message.Text, hasPhoto);
            return route.Intent != MediaToolIntent.None;
        }

        /// <summary>
        /// Regex missed it — for short, private-chat messages ask a cheap LLM classifier
        /// (gpt-4o-mini) to catch phrasings regex can't enumerate (e.g. "нарисуй жирафа").
        /// Fails open (returns false) on any timeout/parse error/"none" verdict.
        /// </summary>
        private async Task<bool> TryLlmFallbackRouteAsync(UpdateDecorator update)
        {
            if (update.IsGroupOrChannel)
            {
                return false;
            }

            if (update.FileId != null)
            {
                return false;
            }

            if (visionImageCache.TryGet(update.UserChatKey, out _))
            {
                return false;
            }

            var text = update.Message?.Text;
            if (!NaturalLanguageToolRouter.LooksLikeShortImperative(text))
            {
                return false;
            }

            var route = await mediaIntentClassifier.TryClassifyAsync(text!, CancellationToken.None);
            return route.Intent != MediaToolIntent.None && await TryRouteMediaToolAsync(update, route);
        }

        /// <summary>
        /// Hands NL media asks off to existing command flows. Returns true when the update
        /// was fully handled (or a follow-up handler was invoked); false to fall through to chat.
        /// </summary>
        private async Task<bool> TryRouteMediaToolAsync(UpdateDecorator update, MediaToolRoute route)
        {
            update.Message.ContextBound = false;
            var profile = await userService.GetUserProfile(update.UserChatKey.Id);

            switch (route.Intent)
            {
                case MediaToolIntent.GenerateImage:
                    if (update.IsGroupOrChannel && !BotMenu.IsAllowedInGroup(CommandType.Image))
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    if (profile is { Images: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessageAsync(update);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.Image);

                    if (!string.IsNullOrWhiteSpace(route.Prompt))
                    {
                        update.Message.Text = route.Prompt;
                        await messageRepository.AddAsync(update.Message);
                        SetNextHandler(imageGeneratorHandler);
                        await base.HandleAsync(update);
                        return true;
                    }

                    await botClient.SendUserReplyAsync(
                        update,
                        string.Format(
                            BotResponse.InputImageDescriptionText,
                            ImageGeneratorHandler.ImageTextDescriptionLimit),
                        TelegramBotUiService.GetImageInstructionInlineKeyboard(false));
                    return true;

                case MediaToolIntent.RecognizeText:
                    if (update.IsGroupOrChannel)
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    if (profile is { ImageTexts: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessageAsync(update);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.TextRecognition);

                    if (update.FileId != null)
                    {
                        await messageRepository.AddAsync(update.Message);
                        SetNextHandler(imageTextRecognitionHandler);
                        await base.HandleAsync(update);
                        return true;
                    }

                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.SendTextRecognitionImage,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return true;

                case MediaToolIntent.PromptFromImage:
                    if (update.IsGroupOrChannel && !BotMenu.IsAllowedInGroup(CommandType.PromptFromImage))
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    if (profile is { GptRequests: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessageAsync(update);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.PromptFromImage);

                    if (update.FileId != null)
                    {
                        await messageRepository.AddAsync(update.Message);
                        SetNextHandler(promptFromImageHandler);
                        await base.HandleAsync(update);
                        return true;
                    }

                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.SendPromptFromImagePhoto,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return true;

                case MediaToolIntent.RemoveWatermark:
                    if (update.IsGroupOrChannel)
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    if (profile.Stars < PaymentConfig.WatermarkRemoval)
                    {
                        await botClient.SendMessage(
                            update.UserChatKey.ChatId,
                            string.Format(BotResponse.InsufficientBalance, PaymentConfig.WatermarkRemoval),
                            replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.RemoveWatermark);

                    if (update.FileId != null)
                    {
                        await messageRepository.AddAsync(update.Message);
                        SetNextHandler(removeWatermarkHandler);
                        await base.HandleAsync(update);
                        return true;
                    }

                    await botClient.SendUserReplyAsync(
                        update,
                        string.Format(BotResponse.RemoveWatermarkIntro, PaymentConfig.WatermarkRemoval),
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return true;

                case MediaToolIntent.StickerPack:
                    if (update.IsGroupOrChannel)
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    if (profile.Stars < StickerPackConfig.HeroStars)
                    {
                        await botClient.SendMessage(
                            update.UserChatKey.ChatId,
                            string.Format(BotResponse.InsufficientBalance, StickerPackConfig.HeroStars),
                            replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.StickerPack);
                    var stickerSession = stickerPackSessionCache.GetOrCreate(update.UserChatKey.Id);
                    stickerSession.Reset();
                    if (update.FileId != null)
                    {
                        stickerSession.SourceFileId = update.FileId;
                    }

                    if (!string.IsNullOrWhiteSpace(route.Prompt))
                    {
                        stickerSession.Description = route.Prompt;
                    }

                    stickerPackSessionCache.Set(update.UserChatKey.Id, stickerSession);

                    if (!string.IsNullOrWhiteSpace(stickerSession.SourceFileId) ||
                        !string.IsNullOrWhiteSpace(stickerSession.Description))
                    {
                        await messageRepository.AddAsync(update.Message);
                        SetNextHandler(stickerPackHandler);
                        await base.HandleAsync(update);
                        return true;
                    }

                    await botClient.SendUserReplyAsync(
                        update,
                        string.Format(BotResponse.StickerPackIntro, StickerPackConfig.HeroStars,
                            StickerPackConfig.PackRemainderStars),
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return true;

                case MediaToolIntent.ChangePhoto:
                    if (update.IsGroupOrChannel)
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.ChangePhoto);
                    if (update.FileId != null)
                    {
                        imageCache.Set(update.UserChatKey.ChatId, update.FileId);
                        await botClient.SendUserReplyAsync(
                            update,
                            BotResponse.SendChangePrompt,
                            TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                        return true;
                    }

                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.SendPhotoToChange,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return true;

                case MediaToolIntent.CombinePhoto:
                    if (update.IsGroupOrChannel)
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.CombinePhoto);
                    var combineSession = aliceImageSessionCache.GetOrCreate(update.UserChatKey.ChatId);
                    combineSession.FirstFileId = null;
                    combineSession.SecondFileId = null;
                    if (update.FileId != null)
                    {
                        combineSession.FirstFileId = update.FileId;
                        aliceImageSessionCache.Set(update.UserChatKey.ChatId, combineSession);
                        await botClient.SendUserReplyAsync(
                            update,
                            BotResponse.SendSecondPhotoToCombine,
                            TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                        return true;
                    }

                    aliceImageSessionCache.Set(update.UserChatKey.ChatId, combineSession);
                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.SendFirstPhotoToCombine,
                        TelegramBotUiService.BackToImagesMenuInlineKeyboard);
                    return true;

                case MediaToolIntent.ImagesMenu:
                    if (update.IsGroupOrChannel)
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.GroupCommandNotAvailable);
                        return true;
                    }

                    await userCommandRepository.AddAsync(update.UserChatKey, CommandType.ImagesMenu);
                    aliceImageSessionCache.Remove(update.UserChatKey.ChatId);
                    imageCache.Remove(update.UserChatKey.ChatId);
                    gptImageSessionCache.Remove(update.UserChatKey.Id);
                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.ChooseImagesPlease,
                        TelegramBotUiService.GetImagesMenuInlineKeyboard());
                    return true;

                case MediaToolIntent.Onboarding:
                    await messageRepository.AddAsync(update.Message);
                    await botClient.SendUserReplyAsync(
                        update,
                        BotResponse.Greeting,
                        TelegramBotUiService.GetOnboardingInlineKeyboard());
                    return true;

                default:
                    return false;
            }
        }

        private async Task<bool> TryStartAliceImageAsync(
            UpdateDecorator update,
            UserChatKey userKey,
            AliceImageKind kind,
            string prompt,
            string imageFileId,
            string? imageFileId2)
        {
            var hold = kind == AliceImageKind.Combining
                ? await userService.TryReserveCombinePhotoAsync(userKey.Id)
                : await userService.TryReserveChangePhotoAsync(userKey.Id);
            if (hold is null)
            {
                var nextExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                await botClient.SendOutOfFreeRequestsMessageAsync(update.ReplyChatId, nextExec);
                return false;
            }

            try
            {
                var progressMessageId = await photoAnimationProgressNotifier.StartImageAsync(update.ReplyChatId);
                await gptService.StartAliceImage(
                    kind,
                    prompt,
                    imageFileId,
                    imageFileId2,
                    update.ReplyChatId,
                    userKey.Id,
                    progressMessageId,
                    hold.Id);
                return true;
            }
            catch
            {
                await userService.ReleaseAsync(hold.Id);
                throw;
            }
        }

        private async Task SendNoFreeRequestsMessageAsync(UpdateDecorator update)
        {
            var nextRefreshLimitExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
            await botClient.SendOutOfFreeRequestsMessageAsync(update.UserChatKey.ChatId, nextRefreshLimitExec);
        }

        private static bool IsGroupFollowUp(UserCommand? lastCommand) =>
            lastCommand?.Type is CommandType.Image
                or CommandType.ImageSquare
                or CommandType.ImageRectangle
                or CommandType.PromptFromImage
                or CommandType.EditImage;

        private Task ResolveInternalUserAsync(UpdateDecorator update)
        {
            var existing = userService.GetByTelegramId(update.TelegramUserId);
            if (existing != null && existing.Id != update.UserChatKey.Id)
            {
                update.BindInternalUserId(existing.Id);
            }

            return Task.CompletedTask;
        }
    }
}
