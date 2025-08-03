using System.Collections.Concurrent;
using GPTipsBot.Resources;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Services;

public class InMemoryAdvertisementTracker(ITelegramBotClient botClient)
{
    private readonly ConcurrentDictionary<long, DateTime> _sentAdvertisements = new();
    private readonly TimeSpan _entryExpiration = TimeSpan.FromDays(3);

    public async Task<bool> TrySendAdvertisement(long userId)
    {
        return await ValueTask.FromResult(true);
        var now = DateTime.Now;
        var isAdvertNotSentYet = _sentAdvertisements.AddOrUpdate(
            userId,
            _ => now,
            (_, existingTime) =>
                now - existingTime > _entryExpiration
                    ? now
                    : existingTime
        ) == now;

        if (isAdvertNotSentYet)
        {
            await botClient.SendMessage(userId, BotResponse.AdvertisementText, ParseMode.MarkdownV2);
        }

        return isAdvertNotSentYet;
    }
}