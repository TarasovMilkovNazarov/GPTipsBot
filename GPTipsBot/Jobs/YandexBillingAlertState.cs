using System.Collections.Concurrent;
using GPTipsBot.Services.YandexCloud;

namespace GPTipsBot.Jobs;

public class YandexBillingAlertState
{
    private readonly ConcurrentDictionary<string, HashSet<decimal>> _notified = new();

    public HashSet<decimal> Snapshot(string accountId)
    {
        var set = _notified.GetOrAdd(accountId, _ => []);
        lock (set)
        {
            return [..set];
        }
    }

    public void Apply(string accountId, IEnumerable<decimal> newlyCrossed, IEnumerable<decimal> recovered)
    {
        var set = _notified.GetOrAdd(accountId, _ => []);
        lock (set)
        {
            foreach (var threshold in newlyCrossed)
                set.Add(threshold);
            foreach (var threshold in recovered)
                set.Remove(threshold);
        }
    }

    private sealed class DailyConsumptionState
    {
        public DateOnly Day;
        public decimal LastBalance;
        public decimal SpentToday;
        public bool Notified;
    }

    private readonly ConcurrentDictionary<string, DailyConsumptionState> _daily = new();

    /// <summary>
    /// Обновляет накопленный расход за сутки по новому значению баланса и решает, нужен ли алерт.
    /// Рост баланса (пополнение) в расход не засчитывается. Счётчик и флаг уведомления
    /// сбрасываются при смене календарного дня (UTC).
    /// </summary>
    public (decimal SpentToday, bool ShouldAlert) TrackDailyConsumption(
        string accountId, decimal currentBalance, DateOnly today)
    {
        var state = _daily.GetOrAdd(accountId, _ => new DailyConsumptionState
        {
            Day = today,
            LastBalance = currentBalance,
        });

        lock (state)
        {
            if (state.Day != today)
            {
                state.Day = today;
                state.SpentToday = 0;
                state.Notified = false;
            }
            else
            {
                var spentSincePoll = state.LastBalance - currentBalance;
                if (spentSincePoll > 0)
                    state.SpentToday += spentSincePoll;
            }

            state.LastBalance = currentBalance;

            var shouldAlert = !state.Notified && YandexBillingBalanceAlerts.DailyConsumptionCrossed(state.SpentToday);
            if (shouldAlert)
                state.Notified = true;

            return (state.SpentToday, shouldAlert);
        }
    }
}
