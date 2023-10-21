using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class CrudHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageRepository messageRepository;
        private readonly UserRepository userRepository;
        private readonly ITelegramBotClient botClient;

        public CrudHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository, ITelegramBotClient botClient, UserRepository userRepository)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<ChatGptHandler>());
            this.userRepository = userRepository;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (MainHandler.userState.ContainsKey(update.UserChatKey) && 
                MainHandler.userState[update.UserChatKey].CurrentState == Enums.UserStateEnum.AwaitingImage)
            {
                update.Message.ContextBound = false;
                SetNextHandler(messageHandlerFactory.Create<ImageGeneratorHandler>());
            }
            if (MainHandler.userState.ContainsKey(update.UserChatKey) && 
                MainHandler.userState[update.UserChatKey].CurrentState == Enums.UserStateEnum.SendingFeedback)
            {
                update.Message.ContextBound = false;
                MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                update.Message.Text = "Отзыв: " + update.Message.Text;
                await botClient.SendTextMessageWithMenuKeyboard(update.UserChatKey.ChatId, BotResponse.Thanks);
                SetNextHandler(null);
            }

            messageRepository.AddMessage(update.Message);

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
