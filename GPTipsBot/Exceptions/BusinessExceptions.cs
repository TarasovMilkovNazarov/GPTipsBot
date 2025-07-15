using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Exceptions;

public class NotSupportedMessageException : Exception
{
    public NotSupportedMessageException(string message) : base($"Unsupported message type '{message}'.")
    {

    }
}

/// <summary>
/// Exception used for ignoring unsupported updates like post reactions, gifs etc
/// </summary>
public class IgnoreMessageTypeException : Exception
{
    public IgnoreMessageTypeException(UpdateType updateType) :
        base($"Silently ignored this message type \"{updateType}\" without user reporting")
    {

    }
}


public class InvalidDepositInputException : Exception
{
    public InvalidDepositInputException(string message) : base(message)
    {

    }
}