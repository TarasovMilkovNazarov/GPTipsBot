namespace GPTipsBot.UpdateHandlers
{
    public interface IMessageHandler<in T>
    {
        Task HandleAsync(T update);
    }
}
