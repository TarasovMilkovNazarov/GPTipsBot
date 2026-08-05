using GPTipsBot.Db;
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
        ImageGeneratorHandler imageGeneratorHandler,
        CommandHandler commandHandler,
        ChatGptHandler chatGptHandler,
        AdminCommandHandler adminCommandHandler,
        ILogger<Dispatcher> logger,
        UserService userService,
        UserCommandRepository userCommandRepository,
        MoneyService moneyService,
        ApplicationContext context,
        BotSettingsRepository botSettingsRepository,
        InvoiceRepository invoiceRepository,
        IGpt gptService,
        IImageCache imageCache,
        MessageRepository messageRepository,
        PhotoAnimationProgressNotifier photoAnimationProgressNotifier)
        : BaseMessageHandler
    {
        public static readonly ConcurrentDictionary<UserChatKey, UserStateDto> UserState = new ();

        public override async Task HandleAsync(UpdateDecorator update)
        {
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
                logger.LogError(ex, "Couldn't create user with telegramId {userId} in database", newUser.Id);
            }

            var language = botSettingsRepository.Get(userKey.Id)?.Language ?? update.Language;
            CultureInfo.CurrentUICulture = new CultureInfo(language);

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
                PaymentCallbacks.TryParsePackage(update.CallbackQuery.Data, out var packageStars))
            {
                await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                await botClient.SendMessage(
                    update.UserChatKey.Id,
                    string.Format(BotResponse.ChoosePaymentMethod, packageStars,
                        MoneyService.FormatRubAmount(packageStars)),
                    replyMarkup: moneyService.BuildPaymentMethodKeyboard(packageStars));
                return;
            }

            if (update.CallbackQuery != null &&
                PaymentCallbacks.TryParse(update.CallbackQuery.Data, out var paymentProvider, out var payStarsCount))
            {
                await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);

                if (paymentProvider == PaymentCallbacks.StarsPrefix)
                {
                    await moneyService.SendInvoice(update.UserChatKey.Id, payStarsCount);
                }
                else if (paymentProvider == PaymentCallbacks.YooKassaPrefix)
                {
                    await moneyService.CreateYooKassaPaymentAsync(
                        update.UserChatKey.Id, payStarsCount, CancellationToken.None);
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
                    await botClient.SendMessage(update.UserChatKey.Id,
                        BotResponse.DonateText, replyMarkup: null);
                }
                else if (confirmResult == PaymentConfirmResult.DepositCredited)
                {
                    var profile = await userService.GetUserProfile(update.UserChatKey.Id);
                    var reply = string.Format(BotResponse.ProfileResponse, profile.FirstName,
                        profile.LastName, profile.Stars, profile.GptRequests, profile.Images, profile.ImageTexts);
                    var replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

                    await botClient.SendMessage(update.UserChatKey.Id, reply, replyMarkup: replyMarkup);
                }

                await userCommandRepository.AddAsync(update.UserChatKey, CommandType.CancelPreviousCommand);

                return;
            }

            var lastCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);

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
            else if (lastCommand?.Type == CommandType.TextRecognition)
            {
                SetNextHandler(imageTextRecognitionHandler);
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

                await botClient.SendMessage(
                    update.UserChatKey.Id,
                    string.Format(BotResponse.ChoosePaymentMethod, starsCount,
                        MoneyService.FormatRubAmount(starsCount)),
                    replyMarkup: moneyService.BuildPaymentMethodKeyboard(starsCount));

                return;
            }
            else if (lastCommand?.Type == CommandType.Donate)
            {
                if (!int.TryParse(update.Message?.Text, out var starsCount) ||
                    starsCount <= 0)
                {
                    throw new ClientCanceledException(update.UserChatKey.ChatId, BotResponse.StarsDonationHint);
                }

                await moneyService.SendDonateInvoice(update.UserChatKey.Id, starsCount);

                return;
            }
            else if (lastCommand?.Type == CommandType.AnimatePhoto)
            {
                var imageId = update.FileId;
                if (update.FileId == null && imageCache.TryGet(update.UserChatKey.ChatId, out imageId) == false)
                {
                    await botClient.SendMessage(update.UserChatKey.ChatId,
                        BotResponse.SendPhotoToAnimate,
                        replyMarkup: TelegramBotUiService.CancelInlineKeyboard);

                    return;
                }

                if (string.IsNullOrWhiteSpace(update.Message?.Text))
                {
                    imageCache.Set(update.UserChatKey.ChatId, imageId!);
                    await botClient.SendMessage(update.UserChatKey.ChatId,
                        BotResponse.SendAnimatePrompt, replyMarkup: TelegramBotUiService.CancelInlineKeyboard);
                    return;
                }

                var progressMessageId = await photoAnimationProgressNotifier.StartAsync(userKey.ChatId);

                await gptService.StartAnimatePhoto(
                    update.Message.Text,
                    imageId!,
                    userKey.ChatId,
                    userKey.Id,
                    progressMessageId);

                imageCache.Remove(userKey.ChatId);
                return;
            }
            else if (update.FileId != null)
            {
                throw new NotSupportedMessageException(update.UserChatKey.ChatId, "Photo");
            }
            else
            {
                SetNextHandler(chatGptHandler);
            }

            await base.HandleAsync(update);
        }
    }
}
