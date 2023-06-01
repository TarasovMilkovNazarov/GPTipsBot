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
            var chatId = update.TelegramGptMessage.UserKey.ChatId;

            if (!TryGetCommand(messageText, out var command))
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            string? responseToUser = null;
            bool keepContext = true;
            IReplyMarkup? replyMarkup = startKeyboard;
            update.TelegramGptMessage.ContextBound = false;

            switch (command)
            {
                case CommandType.Start:
                    MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.None;
                    update.TelegramGptMessage.Source = TelegramService.GetSource(messageText);
                    userRepository.CreateUpdateUser(update.TelegramGptMessage);
                    responseToUser = BotResponse.Greeting;
                    break;
                case CommandType.Help:
                    MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.None;
                    responseToUser = telegramBotAPI.GetMyDescription();
                    break;
                case CommandType.CreateImage:
                    responseToUser = BotResponse.InputImageDescriptionText;
                    replyMarkup = cancelKeyboard;
                    MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.AwaitingImage;
                    break;
                case CommandType.ResetContext:
                    MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.None;
                    responseToUser = BotResponse.ContextUpdated;
                    keepContext = false;
                    break;
                case CommandType.Feedback:
                    responseToUser = BotResponse.SendFeedback;
                    MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.SendingFeedback;
                    replyMarkup = cancelKeyboard;
                    break;
                case CommandType.Cancel:
                    responseToUser = BotResponse.Cancel;
                    MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.None;
                    break;
                case CommandType.StopRequest:
                    responseToUser = BotResponse.Cancel;
                    try
                    {
                        MainHandler.userState[update.TelegramGptMessage.UserKey].messageIdToCancellation[update.Update.CallbackQuery.Message.MessageId].Cancel();
                    }
                    catch (Exception)
                    {
                        return;
                    }
                    
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
            else if (message.Equals("/cancel") || message.Equals("отмена"))
            {
                command = CommandType.Cancel;
            }
            else if (message.Equals("/stoprequest"))
            {
                command = CommandType.StopRequest;
            }
            else
            {
                command = null;
            }

            return command.HasValue;
        }
    }
}
