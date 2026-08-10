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
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services.Cache;
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
        IGptImageSessionCache gptImageSessionCache,
        MessageRepository messageRepository,
        PhotoAnimationProgressNotifier photoAnimationProgressNotifier,
        IJobService jobService)
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

            var language = botSettingsRepository.Get(userKey.Id)?.Language ?? update.Language;
            CultureInfo.CurrentUICulture = new CultureInfo(language);

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
                        profile.PhotoAnimations, profile.Summaries);
                    var replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

                    await botClient.SendUserReplyAsync(update, reply, replyMarkup);
                }

                await userCommandRepository.AddAsync(update.UserChatKey, CommandType.CancelPreviousCommand);

                return;
            }

            if (update.IsAdminCommand())
            {
                SetNextHandler(adminCommandHandler);
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
                            TelegramBotUiService.CancelInlineKeyboard);
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
            else if (update.IsGroupOrChannel &&
                     lastCommand?.Type is CommandType.TextRecognition
                         or CommandType.Deposit
                         or CommandType.Donate
                         or CommandType.AnimatePhoto
                         or CommandType.GptImage)
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
                    await botClient.SendUserReplyAsync(update, BotResponse.SendPhotoToAnimate,
                        TelegramBotUiService.CancelInlineKeyboard);

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
            else if (update.FileId != null)
            {
                throw new NotSupportedMessageException(update.UserChatKey.ChatId, "Photo");
            }
            else if (string.IsNullOrWhiteSpace(update.Message?.Text))
            {
                return;
            }
            else
            {
                SetNextHandler(chatGptHandler);
            }

            await base.HandleAsync(update);
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
