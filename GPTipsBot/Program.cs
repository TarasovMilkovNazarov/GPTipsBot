using GPTipsBot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GPTipsBot.Db;
using Microsoft.Extensions.Configuration;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using OpenAI.GPT3.Extensions;
using GPTipsBot.Api;
using Telegram.Bot;
using GPTipsBot.UpdateHandlers;

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
        .AddScoped<TypingStatus>()
        .AddScoped<MessageHandlerFactory>()
        .AddScoped<MainHandler>()
        .AddScoped<DeleteUserHandler>()
        .AddScoped<OnMaintenanceHandler>()
        .AddScoped<RateLimitingHandler>()
        .AddScoped<MessageTypeHandler>()
        .AddScoped<CrudHandler>()
        .AddScoped<CommandHandler>()
        .AddScoped<GptToUserHandler>()
        .AddScoped<UserToGptHandler>()
        .AddScoped<MessageService>()
        .AddScoped<GptAPI>()
        .AddScoped<ChatGptService>()
        .AddScoped(x => ActivatorUtilities.CreateInstance<TelegramBotAPI>(x, AppConfig.TelegramToken, AppConfig.СhatId))
        .AddScoped<MessageContextRepository>()
        .AddScoped<UserRepository>()
        .AddScoped<ITelegramBotClient, TelegramBotClient>(x => ActivatorUtilities.CreateInstance<TelegramBotClient>(x, AppConfig.TelegramToken));

        services.AddOpenAIService(settings => { settings.ApiKey = AppConfig.OpenAiToken; });
    });

await hostBuilder.RunConsoleAsync();