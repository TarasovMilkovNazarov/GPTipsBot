using System.Collections.Concurrent;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public class RateLimiter
    {
        private readonly ITelegramBotClient _botClient;
        private Timer _resetMessageCountsPerMinuteTimer;
        private Timer _resetMessageCountsPerDayTimer;

        public const int MaxMessagesCountPerMinute = 5;
        public const int MaxMessageCountPerDay = 30;

        private TimeSpan MinuteResetInterval { get; } = TimeSpan.FromSeconds(60);
        private TimeSpan DayResetInterval { get; } = TimeSpan.FromDays(1);

        private ConcurrentDictionary<long, int> UserToDayMessageCount { get; } = new();
        private ConcurrentDictionary<long, int> UserToMinuteMessageCount { get; } = new();
        private readonly object _sync = new object();

        public RateLimiter(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        public bool IsAllowed(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;

            return update.IsCommand || TryIncrementMessageCount(_botClient, chatId);
        }

        private bool IsMinuteLimitOk(long chatId, ITelegramBotClient botClient)
        {
            var value = UserToMinuteMessageCount.GetOrAdd(chatId, 0);

            var diff = value - MaxMessagesCountPerMinute;
            var isBlockingRequest = diff > 0;
            var telegramSpamLimitPass = diff < 2;
            if (isBlockingRequest && telegramSpamLimitPass)
            {
                botClient.SendTextMessageAsync(chatId, BotResponse.TooManyRequests);
            }

            return !isBlockingRequest;
        }

        private bool IsDailyLimitOk(long chatId, ITelegramBotClient botClient)
        {
            var value = UserToDayMessageCount.GetOrAdd(chatId, 0);
            var diff = value - MaxMessageCountPerDay;
            var isBlockingRequest = diff >= 0;
            if (isBlockingRequest && diff < 2)
            {
                var text = string.Format(BotResponse.DailyLimitViolation, MaxMessageCountPerDay);
                botClient.SendTextMessageAsync(chatId, text);
            }
            
            return !isBlockingRequest;
        }

        public RateLimiter()
        {
            _resetMessageCountsPerMinuteTimer = new Timer(ResetMessageCountsPerMinute, null, TimeSpan.Zero,
                MinuteResetInterval);
            _resetMessageCountsPerDayTimer =
                new Timer(ResetMessageCountsPerDay, null, TimeSpan.Zero, DayResetInterval);
        }

        private void ResetMessageCountsPerMinute(object? o)
        {
            UserToMinuteMessageCount.Clear();
        }

        private void ResetMessageCountsPerDay(object? o)
        {
            UserToDayMessageCount.Clear();
        }

        public bool TryIncrementMessageCount(ITelegramBotClient botClient, long chatId)
        {
            lock (_sync)
            {
                IncrementMinuteMessageCount(chatId);
                var isAllLimitsOk = IsMinuteLimitOk(chatId, botClient) && IsDailyLimitOk(chatId, botClient);
                if (!isAllLimitsOk) return false;
                IncrementDailyMessageCount(chatId);
                
                return true;

            }
        }
        private int IncrementMinuteMessageCount(long chatId)
        {
            var minuteCounter = UserToMinuteMessageCount.AddOrUpdate(chatId, 1, (k, v) => Interlocked.Increment(ref v));

            return minuteCounter;
        }
        private int IncrementDailyMessageCount(long chatId)
        {
            var daysCounter = UserToDayMessageCount.AddOrUpdate(chatId, 1, (k, v) => Interlocked.Increment(ref v));

            return daysCounter;
        }
    }
}