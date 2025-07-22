using GPTipsBot.Models;
using Telegram.Bot.Types;

namespace GPTipsBot.Dtos;

public class CustomBotCommand: BotCommand
{
    public CommandType Type { get; set; }
    public bool IsDisabled { get; set; }
}