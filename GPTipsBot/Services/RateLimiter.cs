using System.Collections.Concurrent;
using GPTipsBot.Dtos;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public class RateLimiter
    {
        private readonly ITelegramBotClient _botClient;
        private readonly Timer _resetMessageCountsPerMinuteTimer;

        public const int MaxMessagesCountPerMinute = 5;

        private TimeSpan MinuteResetInterval { get; } = TimeSpan.FromSeconds(60);

        private ConcurrentDictionary<long, int> UserToDayMessageCount { get; } = new();
        private ConcurrentDictionary<long, int> UserToMinuteMessageCount { get; } = new();
        private readonly object _sync = new object();

        public RateLimiter(ITelegramBotClient botClient)
        {
            _botClient = botClient;

            _resetMessageCountsPerMinuteTimer = new Timer(ResetMessageCountsPerMinute, null, TimeSpan.Zero,
                MinuteResetInterval);
        }

        public bool IsAllowed(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;

            return update.IsCommand || TryIncrementMessageCount(chatId);
        }

        private bool IsMinuteLimitOk(long chatId)
        {
            var value = UserToMinuteMessageCount.GetOrAdd(chatId, 0);

            var diff = value - MaxMessagesCountPerMinute;
            var isBlockingRequest = diff > 0;
            var telegramSpamLimitPass = diff < 2;
            if (isBlockingRequest && telegramSpamLimitPass)
            {
                _botClient.SendMessage(chatId, BotResponse.TooManyRequests);
            }

            return !isBlockingRequest;
        }

        private void ResetMessageCountsPerMinute(object? o)
        {
            UserToMinuteMessageCount.Clear();
        }

        public bool TryIncrementMessageCount(long chatId)
        {
            lock (_sync)
            {
                IncrementMinuteMessageCount(chatId);
                return IsMinuteLimitOk(chatId);
            }
        }
        private int IncrementMinuteMessageCount(long chatId)
        {
            var minuteCounter = UserToMinuteMessageCount.AddOrUpdate(chatId, 1, (k, v) => Interlocked.Increment(ref v));

            return minuteCounter;
        }
    }
}