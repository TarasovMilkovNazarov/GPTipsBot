using Microsoft.Extensions.Hosting;
using dotenv.net;
using GPTipsBot.Extensions;
using GPTipsBot.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

DotEnv.Fluent()
    .WithProbeForEnv(10)
    .Load();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging((context, logging) =>
    {
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
        logging.AddConsole((options => options.FormatterName = nameof(JustNormalFormatter)));
        logging.AddConsoleFormatter<JustNormalFormatter, ConsoleFormatterOptions>();
        logging.AddFilter("System.Net.Http.HttpClient", level => level == LogLevel.Error);
    })
    .ConfigureServices((_, services) => { services.ConfigureServices(); })
    .Build();

await host.RunAsync();