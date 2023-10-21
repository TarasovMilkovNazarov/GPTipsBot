using GPTipsBot.Api;
using GPTipsBot.Db;
using GPTipsBot.Localization;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Telegram.Bot.Services;
using Telegram.Bot;
using GPTipsBot.Resources;
using GPTipsBot.Services.ChatGpt;

namespace GPTipsBot.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
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

            services.AddScoped<UpdateHandlerEntryPoint>();
            services.AddScoped<ReceiverService>();
            services.AddHostedService<PollingService>();

            // Add your services with depedency injection.
            services
            .AddTransient<UserService>()
            .AddSingleton<ImageCreatorService>()
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
            .AddTransient<IGpt, GptApi>()
            .AddSingleton<TokenQueue>()
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

            return services;
        }

        static void InitializeBot(ITelegramBotClient botClient, string? langCode = null)
        {
            var botMenu = new BotMenu();
            botClient.SetMyCommandsAsync(botMenu.GetBotCommands(), languageCode: langCode);
            botClient.SetMyNameAsync(AppConfig.Env == "Production" ? BotResponse.BotName : BotResponse.DevBotName, languageCode: langCode);
            botClient.SetMyDescriptionAsync(BotResponse.BotDescription, languageCode: langCode);
            botClient.SetMyShortDescriptionAsync(AppConfig.Env == "Production" ? BotResponse.ShortDescription : BotResponse.ShortDevDescription, languageCode: langCode);
        }
    }
}
