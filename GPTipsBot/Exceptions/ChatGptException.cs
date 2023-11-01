namespace GPTipsBot.Exceptions
{
    /// <summary>
    /// Exception when request to OpeanAi server failed. Usually error occurs on openai server side
    /// </summary>
    public class ChatGptException : Exception
    {
        public ChatGptException(int retryAttempt) : 
            base($"Retry attempt N={retryAttempt} finished with empty result from OpenAi service")
        {

        }
    }
}
