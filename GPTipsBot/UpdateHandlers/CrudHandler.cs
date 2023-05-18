using GPTipsBot.Api;
using GPTipsBot.Repositories;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class CrudHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageContextRepository messageRepository;

        public CrudHandler(MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            SetNextHandler(messageHandlerFactory.Create<UserToGptHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            messageRepository.AddUserMessage(update.TelegramGptMessage);
            if (MainHandler.userState.ContainsKey(update.TelegramGptMessage.TelegramId) && 
                MainHandler.userState[update.TelegramGptMessage.TelegramId] == Enums.UserStateEnum.AwaitingImage)
            {
                SetNextHandler(messageHandlerFactory.Create<ImageGeneratorToUserHandler>());
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
