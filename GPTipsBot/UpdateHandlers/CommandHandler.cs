using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Ardalis.GuardClauses;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;
using Invoice = GPTipsBot.Models.Invoice;

namespace GPTipsBot.UpdateHandlers
{
    using static TelegramBotUiService;
    using static BotMenu;

    public class CommandHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ApplicationContext _context;
        private readonly ILogger<CommandHandler> _logger;
        private readonly MessageRepository _messageRepository;
        private readonly UserCommandRepository _userCommandRepository;
        private readonly ImageGeneratorHandler _imageGeneratorHandler;
        private readonly InvoiceRepository _invoiceRepository;
        private readonly UserService _userService;
        private readonly BotSettingsRepository _botSettingsRepository;

        public CommandHandler(ITelegramBotClient botClient,
            ApplicationContext context, ILogger<CommandHandler> logger, MessageRepository messageRepository,
            UserCommandRepository userCommandRepository, ImageGeneratorHandler imageGeneratorHandler,
            InvoiceRepository invoiceRepository, UserService userService, BotSettingsRepository botSettingsRepository)
        {
            _botClient = botClient;
            _context = context;
            _logger = logger;
            _messageRepository = messageRepository;
            _userCommandRepository = userCommandRepository;
            _imageGeneratorHandler = imageGeneratorHandler;
            _invoiceRepository = invoiceRepository;
            _userService = userService;
            _botSettingsRepository = botSettingsRepository;
        }

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
            var previousCommand = await _userCommandRepository.GetLastAsync(update.UserChatKey);
            await _userCommandRepository.AddAsync(update.UserChatKey, update.Command.Type);

            IReplyMarkup? replyMarkup = StartKeyboard;
            update.Message.ContextBound = false;
            string? reply = null;

            switch (update!.Command.Command)
            {
                case StartCommand:
                    await _botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(),
                        BotCommandScope.Chat(update.UserChatKey.ChatId));
                    reply = BotResponse.Greeting;
                    break;
                case GetProfileCommand:
                    var profile = await _userService.GetUserProfile(update.UserChatKey.Id);
                    reply = String.Format(BotResponse.ProfileResponse, profile.FirstName,
                        profile.LastName, profile.Stars, profile.Images, profile.ImageTexts);
                    replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                        .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));
                    break;
                case DepositCommand:
                    var invoiceMessage = await _botClient.SendInvoiceAsync(
                        chatId: chatId,
                        title: "Premium Content",
                        description: "Access to premium features for 1 month",
                        payload: "premium_monthly_subscription",
                        providerToken: "",
                        currency: Currency.Stars, // XTR is the currency code for Telegram Stars
                        prices: new[] { new LabeledPrice("Premium Access", 1) }, // 1000 Stars = 10 USD
                        startParameter: "premium_subscription");
                    Guard.Against.Null(invoiceMessage.Invoice);
                    _invoiceRepository.Create(new Invoice
                    {
                        CreatedAt = DateTime.UtcNow,
                        UserId = update.UserChatKey.Id,
                        Amount = invoiceMessage.Invoice.TotalAmount,
                        Currency = invoiceMessage.Invoice.Currency,
                        Status = InvoiceStatus.Created,
                        TelegramInvoiceId = invoiceMessage.MessageId,
                    });
                    return;
                case HelpCommand:
                    reply = BotResponse.BotDescription;
                    break;
                case ImageCommand:
                    if (messageText.StartsWith("/image "))
                    {
                        update.Message.Text = messageText.Substring("/image ".Length);
                        SetNextHandler(_imageGeneratorHandler);
                        await _messageRepository.AddAsync(update.Message);
                        await base.HandleAsync(update);
                        return;
                    }
                    reply = String.Format(BotResponse.InputImageDescriptionText, ImageGeneratorHandler.ImageTextDescriptionLimit);
                    replyMarkup = CancelKeyboard;
                    break;
                case ImageTextRecognizeCommand:
                    reply = BotResponse.SendTextRecognitionImage;
                    replyMarkup = CancelKeyboard;
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
                    reply = BotResponse.Cancel;
                    break;
                case StopRequestCommand:
                    reply = BotResponse.Cancel;
                    if (!MainHandler.UserState.TryGetValue(update.UserChatKey, out var state))
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

            await _messageRepository.AddAsync(update.Message);
            await _botClient.SendTextMessageAsync(chatId, reply, replyMarkup: replyMarkup);
            return;

            async Task<string?> UpdateLanguage(UserChatKey userKey, string langCode)
            {
                CultureInfo.CurrentUICulture = new CultureInfo(langCode);

                await _botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(update.UserChatKey.ChatId));
                replyMarkup = new ReplyKeyboardRemove();

                var settings = _botSettingsRepository.Get(userKey.Id);
                if (settings == null)
                {
                    _botSettingsRepository.Create(userKey.Id, langCode);
                }
                else
                {
                    _botSettingsRepository.Update(userKey.Id, langCode);
                }

                return BotResponse.LanguageWasSetSuccessfully;
            }
        }
    }
}
