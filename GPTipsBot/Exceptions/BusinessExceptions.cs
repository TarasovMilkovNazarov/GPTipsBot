using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Exceptions;

public class NotSupportedMessageException : Exception
{
    public NotSupportedMessageException(string message) : base($"Unsupported message type '{message}'.")
    {

    }
}

public class IgnoreMessageTypeException : Exception
{
    public IgnoreMessageTypeException(UpdateType updateType) :
        base($"Silently ignored this message type \"{updateType}\" without user reporting")
    {

    }
}