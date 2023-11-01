namespace GPTipsBot.Exceptions
{
    /// <summary>
    /// Exception for sending to user's chat
    /// </summary>
    public class ClientException : Exception
    {
        public ClientException(string error) : base(error)
        {

        }
    }
}
