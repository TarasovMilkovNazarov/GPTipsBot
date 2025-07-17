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
}
