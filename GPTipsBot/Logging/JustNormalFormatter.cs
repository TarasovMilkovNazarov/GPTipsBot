using GPTipsBot.Extensions;
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

        textWriter.WriteLine($"{now:yyyy-MM-dd HH:mm:ss,fff} " +
                             $"{TransformLevel(logEntry),-5} " +
                             $"{message}");

        if (logEntry.Exception != null)
        {
            textWriter.WriteLine("```stackTrace");
            textWriter.WriteLine(logEntry.Exception.ToString());
            textWriter.WriteLine("```");
        }

        SendAlertIfError(logEntry.LogLevel, textWriter.ToString()!, textWriter);
    }

    private static void SendAlertIfError(LogLevel logLevel, string logText, TextWriter diagnosticLog)
    {
        if (logLevel is not (LogLevel.Critical or LogLevel.Error)) return;

        const string errorPrefix = "🚧🚧🚧 ПАРДОН МЕСЬЕ Я ПРИУНЫЛ:";
        try
        {
            var botClient = new TelegramBotClient(new TelegramBotClientOptions(AppConfig.TelegramToken));
            foreach (var adminId in AppConfig.AdminIds)
            {
                var text = $"{errorPrefix}{Environment.NewLine}{logText}";
                botClient.SendMarkdown2MessageAsync(adminId, text).GetAwaiter().GetResult();
            }
        }
        catch (Exception e1)
        {
            diagnosticLog.WriteLine($"Не смогли отправить сообщение об ошибке админам в телеграм{Environment.NewLine}" +
                                    $"EXCEPTION #1{Environment.NewLine}" +
                                    $"{e1}");
            
            try
            {
                var botClient = new TelegramBotClient(new TelegramBotClientOptions(AppConfig.TelegramToken));
                foreach (var adminId in AppConfig.AdminIds)
                {
                    var text = $"{errorPrefix}{Environment.NewLine}" +
                               $"Не смогли отправить полное сообщение об ошибке в телегу, посмотри его срочно в логах. " +
                               $"Там же будет написано почему оно не попало в телегу";
                    botClient.SendTextMessageAsync(adminId, text).GetAwaiter().GetResult();
                }
            }
            catch (Exception e2)
            {
                diagnosticLog.WriteLine($"Не смогли отправить сообщение об ошибке админам в телеграм{Environment.NewLine}" +
                                        $"EXCEPTION #2{Environment.NewLine}" +
                                        $"{e2}");
            }
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