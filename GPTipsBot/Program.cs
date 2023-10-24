using Microsoft.Extensions.Hosting;
using dotenv.net;
using GPTipsBot.Extensions;
using GPTipsBot.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Serilog;
using Serilog.Events;

DotEnv.Fluent().WithProbeForEnv(10).Load();

var outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Log4NetLevel} {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Error)
    .Enrich.FromLogContext()
    .Enrich.With<Log4NetLevelMapperEnricher>()
    .WriteTo.Console(outputTemplate: outputTemplate)
    .WriteTo.File(
        Path.Combine("logs", "bot.log"), 
        outputTemplate: outputTemplate, 
        fileSizeLimitBytes: 268435456, 
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 3, 
        rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging((context, logging) =>
    {
        // logging.AddConfiguration(context.Configuration.GetSection("Logging"));
        // logging.AddConsole((options => options.FormatterName = nameof(JustNormalFormatter)));
        // logging.AddConsoleFormatter<JustNormalFormatter, ConsoleFormatterOptions>();
        // logging.AddFilter("System.Net.Http.HttpClient", level => level == LogLevel.Error);
    })
    .UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Error)
        .Enrich.FromLogContext()
        .Enrich.With<Log4NetLevelMapperEnricher>()
        .WriteTo.Console(outputTemplate: outputTemplate)
        .WriteTo.File(
            Path.Combine("logs", "bot.log"), 
            outputTemplate: outputTemplate, 
            fileSizeLimitBytes: 268435456, 
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: 3, 
            rollingInterval: RollingInterval.Day))
    .ConfigureServices((_, services) => { services.ConfigureServices(); })
    .Build();

await host.RunAsync();