namespace GPTipsBot.UpdateHandlers
{
    public interface IMessageHandler
    {
        Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken);
    }
}
