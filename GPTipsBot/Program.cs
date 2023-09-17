using GPTipsBot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GPTipsBot.Db;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using GPTipsBot.Api;
using Telegram.Bot;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Services;
using dotenv.net;
using System.Globalization;
using GPTipsBot.Resources;
using GPTipsBot.Localization;
using Microsoft.EntityFrameworkCore;

DotEnv.Fluent().WithProbeForEnv(10).Load();

var host = HostBuilder.CreateHostBuilder(args).Build();

await host.RunAsync();

public class HostBuilder
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
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
            .AddTransient<UserService>()
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
            .AddTransient<GptAPI>()
            .AddScoped<ChatGptService>()
            .AddScoped(x => ActivatorUtilities.CreateInstance<TelegramBotAPI>(x, AppConfig.TelegramToken))
            .AddTransient<MessageRepository>()
            .AddTransient<UserRepository>()
            .AddTransient<BotSettingsRepository>()
            .AddTransient<OpenaiAccountsRepository>()
            .AddTransient<UnitOfWork>()
            .AddSingleton<ITelegramBotClient, TelegramBotClient>(x =>
            {
                var botClient = ActivatorUtilities.CreateInstance<TelegramBotClient>(x, AppConfig.TelegramToken);

                CultureInfo.CurrentUICulture = LocalizationManager.Ru;
                InitializeBot(botClient, "ru");

                CultureInfo.CurrentUICulture = LocalizationManager.En;
                InitializeBot(botClient);

                return botClient;
            });

            services.AddDbContext<ApplicationContext>();
        });
    }

    public static void InitializeBot(ITelegramBotClient botClient, string? langCode = null) {
        var botMenu = new BotMenu();
        botClient.SetMyCommandsAsync(botMenu.GetBotCommands(), languageCode: langCode);
        botClient.SetMyNameAsync(AppConfig.Env == "Production" ? BotResponse.BotName : BotResponse.DevBotName, languageCode: langCode);
        botClient.SetMyDescriptionAsync(BotResponse.BotDescription, languageCode: langCode);
        botClient.SetMyShortDescriptionAsync(AppConfig.Env == "Production" ? BotResponse.ShortDescription : BotResponse.ShortDevDescription, languageCode: langCode);
    }
}

