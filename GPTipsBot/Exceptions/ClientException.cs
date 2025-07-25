namespace GPTipsBot.Exceptions
{
    /// <summary>
    /// Exception for sending to user's chat
    /// </summary>
    public class ClientException(long chatId, string error) : Exception(error)
    {
        public long ChatId { get; } = chatId;
    }

    /// <summary>
    /// Exception for sending to user's chat with inline cancel button
    /// </summary>
    public class ClientCanceledException(long chatId, string error) : Exception(error)
    {
        public long ChatId { get; } = chatId;
    }
}
