namespace GPTipsBot.Exceptions
{
    /// <summary>
    /// Exception for sending to user's chat
    /// </summary>
    public class ClientException : Exception
    {
        public long ChatId { get; }

        public ClientException(long chatId, string error) : base(error)
        {
            ChatId = chatId;
        }
    }

    /// <summary>
    /// Exception for sending to user's chat with inline cancel button
    /// </summary>
    public class ClientCanceledException : Exception
    {
        public long ChatId { get; }

        public ClientCanceledException(long chatId, string error) : base(error)
        {
            ChatId = chatId;
        }
    }
}
