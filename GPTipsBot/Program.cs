using GPTipsBot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GPTipsBot.Db;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using OpenAI.Extensions;
using GPTipsBot.Api;
using Telegram.Bot;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Services;
using dotenv.net;
using System.Globalization;
using GPTipsBot.Resources;
using GPTipsBot.Localization;

DotEnv.Fluent().WithProbeForEnv(10).Load();

var host = Host.CreateDefaultBuilder(args)
        // Add configuration, logging, ...
    .ConfigureServices((hostContext, services) =>
    {
        services.AddLocalization(options =>
        {
            options.ResourcesPath = "Resources";
        });

        services.AddHttpClient("telegram_bot_client")
                .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
                {
                    TelegramBotClientOptions options = new(AppConfig.TelegramToken);
                    return new TelegramBotClient(options);
                });

        services.AddScoped<TelegramBotWorker>();
        services.AddScoped<ReceiverService>();
        services.AddHostedService<PollingService>();

        // Add your services with depedency injection.
        services
        .AddLogging(configure => configure.AddConsole())
        .AddSingleton<DapperContext>()
        .AddTransient<ImageCreatorService>()
        .AddTransient<ActionStatus>()
        .AddTransient<MessageHandlerFactory>()
        .AddTransient<MainHandler>()
        .AddTransient<DeleteUserHandler>()
        .AddTransient<RecoveryHandler>()
        .AddTransient<OnAdminCommandHandler>()
        .AddTransient<RateLimitingHandler>()
        .AddTransient<MessageTypeHandler>()
        .AddTransient<GroupMessageHandler>()
        .AddTransient<CrudHandler>()
        .AddTransient<CommandHandler>()
        .AddTransient<ImageGeneratorHandler>()
        .AddTransient<ChatGptHandler>()
        .AddScoped<MessageService>()
        .AddScoped<GptAPI>()
        .AddScoped<ChatGptService>()
        .AddScoped(x => ActivatorUtilities.CreateInstance<TelegramBotAPI>(x, AppConfig.TelegramToken))
        .AddScoped<MessageRepository>()
        .AddScoped<UserRepository>()
        .AddScoped<BotSettingsRepository>()
        .AddScoped<ITelegramBotClient, TelegramBotClient>(x => {
            var botClient = ActivatorUtilities.CreateInstance<TelegramBotClient>(x, AppConfig.TelegramToken);

            CultureInfo.CurrentUICulture = LocalizationManager.Ru;
            InitializeBot(botClient, "ru");

            CultureInfo.CurrentUICulture = LocalizationManager.En;
            InitializeBot(botClient);

            return botClient;
         });

        services.AddOpenAIService(settings => { settings.ApiKey = AppConfig.OpenAiToken; });
    }).Build();

void InitializeBot(ITelegramBotClient botClient, string? langCode = null) {
    var botMenu = new BotMenu();
    botClient.SetMyCommandsAsync(botMenu.GetBotCommands(), languageCode: langCode);
    botClient.SetMyNameAsync(AppConfig.Env == "Production" ? BotResponse.BotName : BotResponse.DevBotName, languageCode: langCode);
    botClient.SetMyDescriptionAsync(BotResponse.BotDescription, languageCode: langCode);
    botClient.SetMyShortDescriptionAsync(AppConfig.Env == "Production" ? BotResponse.ShortDescription : BotResponse.ShortDevDescription, languageCode: langCode);
}

await host.RunAsync();