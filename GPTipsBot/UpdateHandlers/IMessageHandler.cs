namespace GPTipsBot.UpdateHandlers
{
    public interface IMessageHandler<T>
    {
        Task HandleAsync(T update, CancellationToken cancellationToken);
    }
}
