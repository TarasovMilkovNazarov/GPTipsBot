using RestSharp;
using GPTipsBot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Dapper;
using GPTipsBot.Db;
using Microsoft.Extensions.Configuration;
using GPTipsBot.Repositories;
using GPTipsBot.Services;

var builder = new ConfigurationBuilder()
                 .AddJsonFile($"appsettings.{AppConfig.Env}.json", true, true);
var config = builder.Build();

var hostBuilder = new HostBuilder()
        // Add configuration, logging, ...
    .ConfigureServices((hostContext, services) =>
    {
        // Add your services with depedency injection.
        services
        .AddLogging(configure => configure.AddConsole())
        .AddHostedService<TelegramBotWorker>()
        .AddSingleton<DapperContext>()
        .AddSingleton<IConfiguration>(config)
        //.AddSingleton<MessageService>()
        .AddTransient<IUserRepository, UserRepository>();
    });

var tgBotApi = new TelegramBotAPI(AppConfig.TelegramToken, AppConfig.СhatId);
//tgBotApi.SetMyDescription("DescriptionExample");

await hostBuilder.RunConsoleAsync();