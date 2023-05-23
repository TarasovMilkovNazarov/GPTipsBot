using GPTipsBot.Api;
using GPTipsBot.Enums;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    using static TelegramBotUIService;
    using static BotMenu;

    public class CommandHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageContextRepository messageContextRepository;
        private readonly UserRepository userRepository;
        private readonly ITelegramBotClient botClient;
        private readonly TelegramBotAPI telegramBotAPI;
        private readonly ILogger<CommandHandler> logger;

        public CommandHandler(MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageContextRepository,
            UserRepository userRepository, ITelegramBotClient botClient, TelegramBotAPI telegramBotAPI, ILogger<CommandHandler> logger)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageContextRepository = messageContextRepository;
            this.userRepository = userRepository;
            this.botClient = botClient;
            this.telegramBotAPI = telegramBotAPI;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<CrudHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var messageText = update.TelegramGptMessage.Text;
            var chatId = update.TelegramGptMessage.ChatId;

            if (!TryGetCommand(messageText, out var command))
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            string? responseToUser = null;
            bool keepContext = true;
            IReplyMarkup? replyMarkup = null;
            update.TelegramGptMessage.ContextBound = false;

            switch (command)
            {
                case CommandType.Start:
                    update.TelegramGptMessage.Source = TelegramService.GetSource(messageText);
                    userRepository.CreateUpdateUser(update.TelegramGptMessage);
                    responseToUser = BotResponse.Greeting;
                    replyMarkup = startKeyboard;
                    break;
                case CommandType.Help:
                    responseToUser = telegramBotAPI.GetMyDescription();
                    break;
                case CommandType.CreateImage:
                    responseToUser = BotResponse.InputImageDescriptionText;
                    MainHandler.userState[update.TelegramGptMessage.TelegramId] = UserStateEnum.AwaitingImage;
                    break;
                case CommandType.ResetContext:
                    responseToUser = BotResponse.ContextUpdated;
                    keepContext = false;
                    break;
            }

            if (!string.IsNullOrEmpty(responseToUser))
            {
                messageContextRepository.AddUserMessage(update.TelegramGptMessage, keepContext);
                await botClient.SendTextMessageAsync(chatId, responseToUser, cancellationToken: cancellationToken, replyMarkup: replyMarkup);
            }
        }

        private bool TryGetCommand(string message, out CommandType? command)
        {
            message = message.Trim().ToLower();

            if (message.StartsWith(Start.Command))
            {
                command = CommandType.Start;
            }
            else if (message.Equals(Help.Command) || message.Equals(helpButton.Text.ToLower()))
            {
                command = CommandType.Help;
            }
            else if (message.Equals(Image.Command) || message.Equals(imageButton.Text.ToLower()))
            {
                command = CommandType.CreateImage;
            }
            else if (message.Equals(ResetContext.Command) || message.Equals(resetContextButton.Text.ToLower()))
            {
                command = CommandType.ResetContext;
            }
            else if (message.Equals(Feedback.Command) || message.Equals(feedbackButton.Text.ToLower()))
            {
                command = CommandType.Feedback;
            }
            else
            {
                command = null;
            }

            return command.HasValue;
        }
    }
}
