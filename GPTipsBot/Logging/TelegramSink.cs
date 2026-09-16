using System.Collections.Concurrent;
using GPTipsBot.Config;
using GPTipsBot.Extensions;
using GPTipsBot.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace GPTipsBot.Logging;

public class TelegramSink : ILogEventSink
{
    /// <summary>How long the same alert text stays muted after it was sent once.</summary>
    private static readonly TimeSpan DuplicateCooldown = TimeSpan.FromMinutes(10);

    /// <summary>Sliding window used to cap how many alerts admins can get.</summary>
    private static readonly TimeSpan BudgetWindow = TimeSpan.FromMinutes(10);

    private const int MaxAlertsPerWindow = 10;

    /// <summary>Telegram is slow to fail during an outage; do not stall the logging thread for 100s.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private static readonly Lazy<ITelegramBotClient> BotClient = new(() =>
        new TelegramBotClient(AppConfig.TelegramToken, new HttpClient(new HappProxyClientHandler())));

    private static readonly ConcurrentDictionary<string, DateTime> LastSentByText = new();
    private static readonly object BudgetLock = new();

    private static DateTime _windowStartedAt = DateTime.UtcNow;
    private static int _sentInWindow;
    private static int _suppressedInWindow;
    private static DateTime _lastDeliveryFailureLoggedAt = DateTime.MinValue;

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Exception is TelegramErrorReportException)
            return;

        // Reporting a broken Telegram connection over Telegram cannot work: every failed getUpdates
        // used to produce an error log plus a failed alert, which is how a single Telegram outage
        // turned into hundreds of thousands of log documents.
        if (IsTelegramTransportFailure(logEvent.Exception))
            return;

        var message = logEvent.RenderMessage().Truncate(1000);
        if (!TryReserveSlot(message, out var suppressedSinceLastAlert))
            return;

        try
        {
            var text = $"""
🚧🚧🚧 Я УПАЛ, иди чини:
Подробнее: https://kibana.skolkokomu.ru/app/r/s/80eMV
{message}
""";
            if (suppressedSinceLastAlert > 0)
            {
                text += $"{Environment.NewLine}(ещё {suppressedSinceLastAlert} похожих ошибок подавлено)";
            }

            using var timeout = new CancellationTokenSource(SendTimeout);
            foreach (var adminId in AppConfig.AdminIds)
            {
                BotClient.Value.SendMessage(adminId, text, cancellationToken: timeout.Token)
                    .GetAwaiter().GetResult();
            }
        }
        catch (Exception e)
        {
            LogDeliveryFailure(e);
        }
    }

    private static bool IsTelegramTransportFailure(Exception? exception)
    {
        while (exception != null)
        {
            switch (exception)
            {
                case ApiRequestException { ErrorCode: >= 500 }:
                case RequestException:
                case HttpRequestException:
                case TaskCanceledException:
                    return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }

    /// <summary>
    /// Applies the duplicate cooldown and the per-window budget. Returns false when the alert
    /// should be dropped, otherwise reports how many alerts were dropped since the last send.
    /// </summary>
    private static bool TryReserveSlot(string message, out int suppressedSinceLastAlert)
    {
        suppressedSinceLastAlert = 0;
        var now = DateTime.UtcNow;

        if (LastSentByText.TryGetValue(message, out var lastSentAt) && now - lastSentAt < DuplicateCooldown)
        {
            Interlocked.Increment(ref _suppressedInWindow);
            return false;
        }

        lock (BudgetLock)
        {
            if (now - _windowStartedAt >= BudgetWindow)
            {
                _windowStartedAt = now;
                _sentInWindow = 0;
                _suppressedInWindow = 0;
                PruneCooldowns(now);
            }

            if (_sentInWindow >= MaxAlertsPerWindow)
            {
                _suppressedInWindow++;
                return false;
            }

            _sentInWindow++;
            suppressedSinceLastAlert = _suppressedInWindow;
            _suppressedInWindow = 0;
        }

        LastSentByText[message] = now;
        return true;
    }

    private static void PruneCooldowns(DateTime now)
    {
        foreach (var (text, sentAt) in LastSentByText)
        {
            if (now - sentAt >= DuplicateCooldown)
            {
                LastSentByText.TryRemove(text, out _);
            }
        }
    }

    /// <summary>
    /// A failed alert is an operational nuisance, not a Fatal condition, and it must not be logged
    /// once per failed send or it re-creates the very storm this sink is trying to report on.
    /// </summary>
    private static void LogDeliveryFailure(Exception e)
    {
        var now = DateTime.UtcNow;
        lock (BudgetLock)
        {
            if (now - _lastDeliveryFailureLoggedAt < DuplicateCooldown)
            {
                return;
            }

            _lastDeliveryFailureLoggedAt = now;
        }

        Log.Logger.Warning(new TelegramErrorReportException(e), "Не смогли отправить сообщение об ошибке в телеграмм");
    }
}
