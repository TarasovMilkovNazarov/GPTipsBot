using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class UserStateHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageRepository messageRepository;
        private readonly ITelegramBotClient botClient;

        public UserStateHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository, ITelegramBotClient botClient)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<ChatGptHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
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
                await botClient.SendTextMessageWithMenuKeyboard(update.UserChatKey.ChatId, BotResponse.Thanks, cancellationToken);
                SetNextHandler(null);
            }
            if (MainHandler.userState.ContainsKey(update.UserChatKey) && 
                MainHandler.userState[update.UserChatKey].CurrentState == Enums.UserStateEnum.AwaitingFaceSwapImages)
            {
                update.Message.ContextBound = false;
                SetNextHandler(messageHandlerFactory.Create<FaceSwapHandler>());
            }

            messageRepository.AddMessage(update.Message);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
