using OpenAI.GPT3.Managers;
using OpenAI.GPT3;
using RestSharp;
using OpenAI.GPT3.ObjectModels;
using GPTipsBot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var hostBuilder = new HostBuilder()
        // Add configuration, logging, ...
    .ConfigureServices((hostContext, services) =>
    {
        // Add your services with depedency injection.
        services
        .AddLogging(configure => configure.AddConsole())
        .AddHostedService<TelegramBotWorker>();
    });

await hostBuilder.RunConsoleAsync();

var tgBotApi = new TelegramBotAPI(AppConfig.TelegramToken, AppConfig.СhatId);
tgBotApi.SetMyDescription("DescriptionExample");