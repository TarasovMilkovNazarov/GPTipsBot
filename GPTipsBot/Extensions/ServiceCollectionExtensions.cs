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
using GPTipsBot.Services.YandexCloud;

namespace GPTipsBot.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });

            services.AddHttpClient("telegram_bot_client")
                    .AddTypedClient<ITelegramBotClient>((httpClient, _) =>
                    {
                        TelegramBotClientOptions options = new(AppConfig.TelegramToken);
                        return new TelegramBotClient(options);
                    });

            services.AddScoped<UpdateFirewall>();
            services.AddScoped<ReceiverService>();
            services.AddHostedService<PollingService>();

            services
            // handlers
            .AddTransient<MainHandler>()
            .AddTransient<RecoveryNotificationHandler>()
            .AddTransient<AdminCommandHandler>()
            .AddTransient<CommandHandler>()
            .AddTransient<ImageGeneratorHandler>()
            .AddTransient<ImageTextRecognitionHandler>()
            .AddTransient<ChatGptHandler>()
            // services
            .AddTransient<UserService>()
            .AddSingleton<TelejetAdClient>()
            .AddSingleton<GramadsAdvertisementClient>()
            .AddSingleton<ImageCreatorService>()
            .AddSingleton<IImageGenerator, YaCloudClient>()
            .AddSingleton<ITextRecognizer, YaCloudClient>()
            .AddTransient<UserStatusActivator>()
            .AddSingleton<SpeechToTextService>()
            .AddTransient<RateLimiter>()
            .AddTransient<IGpt, ChatGptService>()
            .AddSingleton<TokenQueue>()
            .AddScoped<ChatGptService>()
            .AddTransient<OpenAiServiceCreator, ProxyApiService>()
            .AddTransient<ContextWindow>()
            .AddRepositories()
            .AddTransient<MoneyService>()
            .AddTransient<UnitOfWork>()
            .AddSingleton<ITelegramBotClient>(x =>
            {
                var botClient = ActivatorUtilities.CreateInstance<TelegramBotClient>(x, AppConfig.TelegramToken);

                CultureInfo.CurrentUICulture = LocalizationManager.Ru;
                InitializeBot(botClient, "ru");

                CultureInfo.CurrentUICulture = LocalizationManager.En;
                InitializeBot(botClient);

                return botClient;
            })
            ;

            services.AddDbContext<ApplicationContext>();

            return services;
        }

        private static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            return services.AddTransient<MessageRepository>()
                .AddTransient<UserRepository>()
                .AddTransient<UserCommandRepository>()
                .AddTransient<BotSettingsRepository>()
                .AddTransient<OpenaiAccountsRepository>()
                .AddTransient<WalletRepository>()
                .AddTransient<TransactionRepository>()
                .AddTransient<InvoiceRepository>();
        }

        static void InitializeBot(ITelegramBotClient botClient, string? langCode = null)
        {
            var botMenu = new BotMenu();
            botClient.SetMyCommandsAsync(botMenu.GetBotCommands(), languageCode: langCode);
            botClient.SetMyNameAsync(AppConfig.IsProduction ? BotResponse.BotName : BotResponse.DevBotName, languageCode: langCode);
            botClient.SetMyDescriptionAsync(BotResponse.BotDescription, languageCode: langCode);
            botClient.SetMyShortDescriptionAsync(AppConfig.IsProduction ? BotResponse.ShortDescription :
                BotResponse.ShortDevDescription, languageCode: langCode);
        }
    }
}
