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
}
