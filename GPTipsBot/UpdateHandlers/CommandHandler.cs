using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    using static TelegramBotUiService;
    using static BotMenu;

    public class CommandHandler(
        ITelegramBotClient botClient,
        ApplicationContext context,
        ILogger<CommandHandler> logger,
        MessageRepository messageRepository,
        UserCommandRepository userCommandRepository,
        ImageGeneratorHandler imageGeneratorHandler,
        InvoiceRepository invoiceRepository,
        UserService userService,
        BotSettingsRepository botSettingsRepository,
        MoneyService moneyService,
        IJobService jobService)
        : BaseMessageHandler
    {
        private readonly ApplicationContext _context = context;
        private readonly ILogger<CommandHandler> _logger = logger;
        private readonly InvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly MoneyService _moneyService = moneyService;

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var messageText = update.Message.Text;
            var chatId = update.UserChatKey.ChatId;

            if (!update.IsCommand)
            {
                await base.HandleAsync(update);
                return;
            }

            Guard.Against.Null(update.Command);
            var previousCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
            await userCommandRepository.AddAsync(update.UserChatKey, update.Command.Type);

            var profile = await userService.GetUserProfile(update.UserChatKey.Id);

            ReplyMarkup replyMarkup = StartKeyboard;
            update.Message.ContextBound = false;
            string? reply = null;

            switch (update!.Command.Command)
            {
                case StartCommand:
                    await botClient.SetMyCommands(new BotMenu().GetBotCommands(),
                        BotCommandScope.Chat(chatId));
                    reply = BotResponse.Greeting;
                    break;
                case GetProfileCommand:
                    reply = string.Format(BotResponse.ProfileResponse, profile.FirstName,
                        profile.LastName, profile.Stars, profile.GptRequests, profile.Images, profile.ImageTexts);
                    replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, DepositCommand));
                    break;
                case DepositCommand:
                    if (update.CallbackQuery == null)
                    {
                        await botClient.SendMessage(update.UserChatKey.ChatId,
                            string.Format(BotResponse.DepositResponse, PaymentConfig.MinRechargeAmount),
                            replyMarkup: CancelInlineKeyboard);
                    }
                    else
                    {
                        await botClient.EditMessageText(update.UserChatKey.ChatId, (int)update.Message.TelegramMessageId!,
                            string.Format(BotResponse.DepositResponse, PaymentConfig.MinRechargeAmount),
                            replyMarkup: CancelInlineKeyboard);
                    }

                    return;
                case DonateCommand:
                    await botClient.SendMessage(update.UserChatKey.ChatId,
                        BotResponse.DonateInstructions, replyMarkup: CancelInlineKeyboard);
                    return;
                case MusicCommand:
                    replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, DepositCommand));
                    await botClient.SendMessage(update.UserChatKey.ChatId, BotResponse.MusicResponse,
                        replyMarkup: replyMarkup);
                    return;
                case SongCommand:
                    if (update.Command.IsDisabled)
                    {
                        return;
                    }
                    replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, DepositCommand));
                    await botClient.SendMessage(chatId, BotResponse.SongResponse,
                        replyMarkup: replyMarkup);
                    return;
                case VideoCommand:
                    replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, DepositCommand));
                    await botClient.SendMessage(chatId, string.Format(BotResponse.VideoInstructions, 60),
                        replyMarkup: replyMarkup);
                    return;
                case HelpCommand:
                    reply = BotResponse.BotDescription;
                    break;
                case ImageCommand:
                    if (profile is { Images: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessage(chatId);
                        return;
                    }

                    if (messageText.StartsWith("/image "))
                    {
                        update.Message.Text = messageText.Substring("/image ".Length);
                        SetNextHandler(imageGeneratorHandler);
                        await messageRepository.AddAsync(update.Message);
                        await base.HandleAsync(update);
                        return;
                    }

                    reply = string.Format(
                        BotResponse.InputImageDescriptionText,
                        ImageGeneratorHandler.ImageTextDescriptionLimit);
                    replyMarkup = GetImageInstructionInlineKeyboard(false);
                    break;
                case ImageSquareCommand:
                    if (previousCommand?.Type == CommandType.ImageSquare)
                    {
                        return;
                    }
                    await botClient.EditMessageReplyMarkup(update.UserChatKey.ChatId,
                        (int)update.Message.TelegramMessageId!.Value,
                        replyMarkup: GetImageInstructionInlineKeyboard(true));
                    return;
                case ImageRectangleCommand:
                    if (previousCommand?.Type == CommandType.ImageRectangle)
                    {
                        return;
                    }
                    await botClient.EditMessageReplyMarkup(update.UserChatKey.ChatId,
                        (int)update.Message.TelegramMessageId!.Value,
                        replyMarkup: GetImageInstructionInlineKeyboard(false));
                    return;
                case ImageTextRecognizeCommand:
                    if (profile is { ImageTexts: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessage(chatId);
                        return;
                    }

                    reply = BotResponse.SendTextRecognitionImage;
                    replyMarkup = CancelInlineKeyboard;
                    break;
                case ResetContextCommand:
                    reply = BotResponse.ContextUpdated;
                    update.Message.NewContext = true;
                    break;
                case ChooseLangCommand:
                    reply = BotResponse.ChooseLanguagePlease;
                    replyMarkup = ChooseLangKeyboard;
                    break;
                case SetEngLangCommand:
                    reply = await UpdateLanguage(update.UserChatKey, "en");
                    break;
                case SetRuLangCommand:
                    reply = await UpdateLanguage(update.UserChatKey, "ru");
                    break;
                case CancelCommand:
                    if (update.CallbackQuery == null)
                    {
                        reply = BotResponse.ContinueConversation;
                    }
                    else if (update.Message.TelegramMessageId.HasValue)
                    {
                        await botClient.EditMessageText(update.UserChatKey.ChatId,
                            (int)update.Message.TelegramMessageId.Value, BotResponse.ContinueConversation);
                        return;
                    };

                    break;
                case StopRequestCommand:
                    reply = BotResponse.Cancel;
                    if (!Dispatcher.UserState.TryGetValue(update.UserChatKey, out var state))
                    {
                        break;
                    }

                    if (previousCommand?.Type is CommandType.Image or CommandType.TextRecognition)
                    {
                        replyMarkup = CancelKeyboard;
                    }
                    else
                    {
                        replyMarkup = new ReplyKeyboardRemove();
                    }

                    if (update.Message.TelegramMessageId.HasValue && state.MessageIdToCancellation
                            .ContainsKey(update.Message.TelegramMessageId.Value))
                    {
                        state.MessageIdToCancellation[update.Message.TelegramMessageId.Value].Cancel();
                    }

                    break;
            }

            Guard.Against.Null(reply);

            await messageRepository.AddAsync(update.Message);
            await botClient.SendMessage(chatId, reply, replyMarkup: replyMarkup);
            return;

            async Task<string?> UpdateLanguage(UserChatKey userKey, string langCode)
            {
                CultureInfo.CurrentUICulture = new CultureInfo(langCode);

                await botClient.SetMyCommands(new BotMenu().GetBotCommands(), BotCommandScope.Chat(chatId));
                replyMarkup = new ReplyKeyboardRemove();

                var settings = botSettingsRepository.Get(userKey.Id);
                if (settings == null)
                {
                    botSettingsRepository.Create(userKey.Id, langCode);
                }
                else
                {
                    botSettingsRepository.Update(userKey.Id, langCode);
                }

                return BotResponse.LanguageWasSetSuccessfully;
            }
        }

        private async Task SendNoFreeRequestsMessage(long chatId)
        {
            var nextRefreshLimitExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
            await botClient.SendOutOfFreeRequestsMessageAsync(chatId, nextRefreshLimitExec);
        }
    }
}
