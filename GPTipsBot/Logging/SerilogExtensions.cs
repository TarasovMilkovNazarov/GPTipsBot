using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace GPTipsBot.Logging;

public static class SerilogExtensions
{
    private const string OutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Log4NetLevel} {Message:lj}{NewLine}{Exception}";

    public static LoggerConfiguration AddBotLogger(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration? configuration = null,
        IServiceProvider? servicesProvider = null)
    {
        if (configuration is not null)
            loggerConfiguration.ReadFrom.Configuration(configuration);

        if (servicesProvider is not null)
            loggerConfiguration.ReadFrom.Services(servicesProvider);

        return loggerConfiguration
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Error)
            .Enrich.FromLogContext()
            .Enrich.With<Log4NetLevelMapperEnricher>()
            .WriteToJsonConsole();
        // .WriteToJsonFile();
    }

    private static LoggerConfiguration WriteToJsonConsole(this LoggerConfiguration loggerConfiguration) =>
        loggerConfiguration.WriteTo.Console(new RenderedCompactJsonFormatter());

    private static LoggerConfiguration WriteToJsonFile(this LoggerConfiguration loggerConfiguration) =>
        loggerConfiguration
            .WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine("logs", "bot.json"),
                fileSizeLimitBytes: 268435456,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 3,
                rollingInterval: RollingInterval.Day);
}