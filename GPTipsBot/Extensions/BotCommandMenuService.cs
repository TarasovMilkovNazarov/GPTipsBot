using System.Collections.Concurrent;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.Extensions;

public class BotCommandMenuService(ITelegramBotClient botClient)
{
    private static readonly ConcurrentDictionary<long, byte> GroupMenusApplied = new();

    /// <summary>
    /// Overwrites a per-chat command menu that may have been set earlier with the full private list.
    /// </summary>
    public async Task EnsureGroupMenuAsync(long chatId, CancellationToken cancellationToken = default)
    {
        if (!GroupMenusApplied.TryAdd(chatId, 0))
        {
            return;
        }

        await botClient.SetMyCommands(
            new BotMenu().GetGroupBotCommands(),
            BotCommandScope.Chat(chatId),
            cancellationToken: cancellationToken);
    }
}
