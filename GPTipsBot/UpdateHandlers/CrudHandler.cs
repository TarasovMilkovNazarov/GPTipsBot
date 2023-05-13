using GPTipsBot.Api;
using GPTipsBot.Repositories;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class CrudHandler : BaseMessageHandler
    {
        private readonly MessageContextRepository messageRepository;

        public CrudHandler(MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository)
        {
            this.messageRepository = messageRepository;
            SetNextHandler(messageHandlerFactory.Create<UserToGptHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            messageRepository.AddUserMessage(update.TelegramGptMessage);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
