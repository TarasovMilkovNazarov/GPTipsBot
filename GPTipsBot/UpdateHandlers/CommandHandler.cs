using GPTipsBot.Api;
using GPTipsBot.Enums;
using GPTipsBot.Mapper;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
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
        private readonly MessageRepository messageContextRepository;
        private readonly UserRepository userRepository;
        private readonly ITelegramBotClient botClient;
        private readonly TelegramBotAPI telegramBotAPI;
        private readonly ILogger<CommandHandler> logger;

        public CommandHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageContextRepository,
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

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            var messageText = update.Message.Text;
            var chatId = update.UserChatKey.ChatId;

            if (!TryGetCommand(messageText, out var command))
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            string? responseToUser = null;
            bool keepContext = true;
            IReplyMarkup? replyMarkup = startKeyboard;
            update.Message.ContextBound = false;

            switch (command)
            {
                case CommandType.Start:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    update.User.Source = TelegramService.GetSource(messageText);
                    userRepository.CreateUpdateUser(UserMapper.Map(update.User));
                    responseToUser = BotResponse.Greeting;
                    break;
                case CommandType.Help:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    responseToUser = BotResponse.BotDescription;
                    break;
                case CommandType.CreateImage:
                    responseToUser = String.Format(BotResponse.InputImageDescriptionText, ImageGeneratorHandler.basedOnExperienceInputLengthLimit);
                    replyMarkup = cancelKeyboard;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingImage;
                    break;
                case CommandType.ResetContext:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    responseToUser = BotResponse.ContextUpdated;
                    keepContext = false;
                    break;
                case CommandType.Feedback:
                    responseToUser = BotResponse.SendFeedback;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.SendingFeedback;
                    replyMarkup = cancelKeyboard;
                    break;
                case CommandType.Cancel:
                    responseToUser = BotResponse.Cancel;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    break;
                case CommandType.StopRequest:
                    responseToUser = BotResponse.Cancel;
                    try
                    {
                        var state = MainHandler.userState[update.UserChatKey];
                        if (state.CurrentState == UserStateEnum.None)
                        { 
                            replyMarkup = new ReplyKeyboardRemove();
                        }

                        state.messageIdToCancellation[update.Message.TelegramMessageId.Value].Cancel();
                    }
                    catch (Exception ex) { return; }
                    
                    break;
            }

            if (!string.IsNullOrEmpty(responseToUser))
            {
                messageContextRepository.AddMessage(update.Message, keepContext: keepContext);
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
            else if (message.Equals(Help.Command) || ButtonToLocalizations[helpButton].Any(b => b.ToLower() == message))
            {
                command = CommandType.Help;
            }
            else if (message.Equals(Image.Command) || ButtonToLocalizations[imageButton].Any(b => b.ToLower() == message))
            {
                command = CommandType.CreateImage;
            }
            else if (message.Equals(ResetContext.Command) || ButtonToLocalizations[resetContextButton].Any(b => b.ToLower() == message))
            {
                command = CommandType.ResetContext;
            }
            else if (message.Equals(Feedback.Command) || ButtonToLocalizations[feedbackButton].Any(b => b.ToLower() == message))
            {
                command = CommandType.Feedback;
            }
            else if (message.Equals("/cancel") || ButtonToLocalizations[cancelButton].Any(b => b.ToLower() == message))
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
