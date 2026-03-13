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
        MessageRepository messageRepository)
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
                if (update.PreCheckoutQuery.InvoicePayload.StartsWith("donate"))
                {
                    await botClient.SendMessage(update.UserChatKey.Id,
                        BotResponse.DonateText, replyMarkup: null);

                    await botClient.AnswerPreCheckoutQuery(
                        preCheckoutQueryId: update.PreCheckoutQuery.Id);
                }
                else
                {
                    await botClient.AnswerPreCheckoutQuery(
                        preCheckoutQueryId: update.PreCheckoutQuery.Id);

                    await moneyService.AddMoneyAsync(update.UserChatKey.Id, update.PreCheckoutQuery.TotalAmount,
                        "TRX", CancellationToken.None);
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
            else if (lastCommand?.Type == CommandType.Video)
            {
                var isSuccessPayment = await moneyService.TryPay(update.UserChatKey.Id, PaymentConfig.Video);
                if (!isSuccessPayment)
                {
                    return;
                }

                await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.PleaseWaitVideoMsg,
                    replyMarkup: null);
                var video = await gptService.GenerateVideoByText(update.Message!.Text, update.FileId, CancellationToken.None);
                await botClient.SendVideo(update.UserChatKey.Id, InputFile.FromUri(video));
                return;
            }
            else if (lastCommand?.Type == CommandType.TextRecognition)
            {
                SetNextHandler(imageTextRecognitionHandler);
            }
            else if (lastCommand?.Type == CommandType.Deposit)
            {
                if (!int.TryParse(update.Message?.Text, out var starsCount) ||
                    starsCount < PaymentConfig.MinRechargeAmount)
                {
                    throw new ClientCanceledException(update.UserChatKey.ChatId,
                        string.Format(BotResponse.InvalidDepositAmountResponse, PaymentConfig.MinRechargeAmount));
                }

                await moneyService.SendInvoice(update.UserChatKey.Id, starsCount);

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
            else if (lastCommand?.Type == CommandType.Music)
            {
                var isSuccessPayment = await moneyService.TryPay(update.UserChatKey.Id, PaymentConfig.Music);
                if (!isSuccessPayment)
                {
                    return;
                }

                var audio = await gptService.GenerateMusicByText(update.Message!.Text, CancellationToken.None);
                await botClient.SendAudio(update.UserChatKey.Id, InputFile.FromStream(audio));
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

                var video = await gptService.AnimatePhoto(update.Message.Text, imageId);
                await botClient.SendVideo(update.UserChatKey.Id, InputFile.FromUri(video));

                await messageRepository.AddAsync(new MessageDto(update.UserChatKey)
                {
                    TelegramId = update.UserChatKey.Id,
                    Role = MessageOwner.Ya,
                    BotMessageType = BotMessageType.AnimatedPhoto,
                });

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
