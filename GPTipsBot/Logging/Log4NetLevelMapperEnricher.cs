using Serilog.Core;
using Serilog.Events;

namespace GPTipsBot.Logging;

// https://stackoverflow.com/questions/51629436/how-do-i-modify-serilog-log-levels-to-match-up-with-log4net
public class Log4NetLevelMapperEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var log4NetLevel = logEvent.Level switch
        {
            LogEventLevel.Debug => "DEBUG",
            LogEventLevel.Error => "ERROR",
            LogEventLevel.Fatal => "FATAL",
            LogEventLevel.Information => "INFO",
            LogEventLevel.Verbose => "TRACE",
            LogEventLevel.Warning => "WARN",
            _ => throw new ArgumentOutOfRangeException(nameof(logEvent.Level))
        };

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Log4NetLevel", log4NetLevel));
    }
}