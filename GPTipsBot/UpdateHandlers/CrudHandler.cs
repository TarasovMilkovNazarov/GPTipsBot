using GPTipsBot.Repositories;

namespace GPTipsBot.UpdateHandlers
{
    public class CrudHandler : BaseMessageHandler
    {
        private readonly HandlerFactory messageHandlerFactory;
        private readonly MessageRepository messageRepository;

        public CrudHandler(HandlerFactory messageHandlerFactory, MessageRepository messageRepository)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            SetNextHandler(messageHandlerFactory.Create<ChatGptHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (MainHandler.userState.ContainsKey(update.UserChatKey) && 
                MainHandler.userState[update.UserChatKey].CurrentState == Enums.UserStateEnum.AwaitingImage)
            {
                update.Message.ContextBound = false;
                SetNextHandler(messageHandlerFactory.Create<ImageGeneratorHandler>());
            }

            messageRepository.AddMessage(update.Message);

            await base.HandleAsync(update);
        }
    }
}
