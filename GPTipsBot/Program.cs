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
        //.AddHttpClient<UnreliableEndpointCallerService>()
        //.AddTransientHttpErrorPolicy(
        //    x => x.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(3, retryAttempt)))
        .AddLogging(configure => configure.AddConsole())
        .AddHostedService<TelegramBotWorker>()
        .AddSingleton<DapperContext>()
        .AddSingleton<IConfiguration>(config)
        .AddTransient<ImageCreatorService>()
        .AddTransient<ImageService>()
        .AddTransient<ActionStatus>()
        .AddTransient<MessageHandlerFactory>()
        .AddTransient<MainHandler>()
        .AddTransient<DeleteUserHandler>()
        .AddTransient<RecoveryHandler>()
        .AddTransient<OnMaintenanceHandler>()
        .AddTransient<RateLimitingHandler>()
        .AddTransient<MessageTypeHandler>()
        .AddTransient<GroupMessageHandler>()
        .AddTransient<CrudHandler>()
        .AddTransient<CommandHandler>()
        .AddTransient<ImageGeneratorToUserHandler>()
        .AddTransient<GptToUserHandler>()
        .AddTransient<UserToGptHandler>()
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