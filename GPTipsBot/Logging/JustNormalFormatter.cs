using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace GPTipsBot.Logging;

public class JustNormalFormatter : ConsoleFormatter, IDisposable
{
    private readonly IDisposable? optionsReloadToken;
    private ConsoleFormatterOptions formatterOptions;

    public JustNormalFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
        : base(nameof(JustNormalFormatter))
    {
        (optionsReloadToken, formatterOptions) =
            (options.OnChange(ReloadLoggerOptions), options.CurrentValue);
    }

    private void ReloadLoggerOptions(ConsoleFormatterOptions options) =>
        formatterOptions = options;

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var now = formatterOptions.UseUtcTimestamp
            ? DateTime.UtcNow
            : DateTime.Now;

        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        var lotText =
            $"{now:yyyy-MM-dd HH:mm:ss,fff} " +
            $"{TransformLevel(logEntry),-5} " +
            $"{message}";
        textWriter.WriteLine(lotText);

        AlertIfError(logEntry.LogLevel, lotText);
    }

    private void AlertIfError(LogLevel logLevel, string logText)
    {
        if (logLevel is LogLevel.Critical or LogLevel.Error)
        {
            var botClient = new TelegramBotClient(new TelegramBotClientOptions(AppConfig.TelegramToken));
            foreach (var adminId in AppConfig.AdminIds)
                botClient.SendTextMessageAsync(adminId, $"🚧 ПАМАГИ МНЕ ДРУГ, Я СЛОМАЛСЯ:{Environment.NewLine}{logText}");
        }
    }

    private static string TransformLevel<TState>(LogEntry<TState> logEntry) =>
        logEntry.LogLevel switch
        {
            LogLevel.Information => "INFO",
            LogLevel.Critical => "FATAL",
            LogLevel.Warning => "WARN",
            _ => logEntry.LogLevel.ToString("G")
                .ToUpperInvariant()
        };

    public void Dispose() => optionsReloadToken?.Dispose();
}