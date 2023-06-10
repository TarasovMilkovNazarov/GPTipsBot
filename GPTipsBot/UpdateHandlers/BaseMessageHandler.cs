namespace GPTipsBot.UpdateHandlers
{
    public abstract class BaseMessageHandler : IMessageHandler<UpdateDecorator>
    {
        private BaseMessageHandler? nextHandler;

        public void SetNextHandler(BaseMessageHandler handler)
        {
            nextHandler = handler;
        }

        public virtual async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            if (nextHandler != null)
            {
                await nextHandler.HandleAsync(update, cancellationToken);
            }
        }
    }
}
