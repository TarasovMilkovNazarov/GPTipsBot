namespace GPTipsBot.Exceptions;

public class NotSupportedMessageException : Exception
{
    public NotSupportedMessageException(string message) : base($"Unsupported message type '{message}'.")
    {

    }
}