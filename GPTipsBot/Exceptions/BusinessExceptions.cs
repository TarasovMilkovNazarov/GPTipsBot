using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Exceptions;

public class NotSupportedMessageException(long chatId, string message)
    : Exception($"Unsupported message type '{message}'.")
{
    public long ChatId { get; } = chatId;
}

/// <summary>
/// Exception used for ignoring unsupported updates like post reactions, gifs etc
/// </summary>
public class IgnoreMessageTypeException(UpdateType updateType)
    : Exception($"Silently ignored this message type \"{updateType}\" without user reporting");


public class InvalidDepositInputException(string message) : Exception(message);