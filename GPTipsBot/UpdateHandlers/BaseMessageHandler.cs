namespace GPTipsBot.UpdateHandlers
{
    public abstract class BaseMessageHandler : IMessageHandler<UpdateDecorator>
    {
        private BaseMessageHandler? _nextHandler;

        protected void SetNextHandler(BaseMessageHandler? handler)
        {
            _nextHandler = handler;
        }

        public virtual async Task HandleAsync(UpdateDecorator update)
        {
            if (_nextHandler != null)
            {
                await _nextHandler.HandleAsync(update);
            }
        }
    }
}
