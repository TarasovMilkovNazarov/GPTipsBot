using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public abstract class BaseMessageHandler : IMessageHandler
    {
        private BaseMessageHandler nextHandler;

        public void SetNextHandler(BaseMessageHandler handler)
        {
            nextHandler = handler;
        }

        public virtual async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (nextHandler != null)
            {
                await nextHandler.HandleAsync(update, cancellationToken);
            }
        }
    }
}
