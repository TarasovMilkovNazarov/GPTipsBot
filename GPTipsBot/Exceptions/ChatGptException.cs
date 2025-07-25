namespace GPTipsBot.Exceptions
{
    /// <summary>
    /// Exception when request to OpeanAi server failed. Usually error occurs on openai server side
    /// </summary>
    public class ChatGptException(int retryAttempt)
        : Exception($"Retry attempt N={retryAttempt} finished with empty result from OpenAi service");
}
