using GPTipsBot.Db;
using GPTipsBot.Localization;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net.Http.Headers;
using GPTipsBot.Config;
using GPTipsBot.Jobs;
using Telegram.Bot.Services;
using Telegram.Bot;
using GPTipsBot.Resources;
using GPTipsBot.Services.Cache;
using GPTipsBot.Services.YandexCloud;
using GPTipsBot.Services.YandexPhotoAnimator;
using GPTipsBot.Services.YandexPhotoAnimator.Workflow;
using GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;
using WorkflowCore.Interface;
using WorkflowCore.Persistence.PostgreSQL;
using Quartz;

namespace GPTipsBot.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            services.AddWorkflow(cfg => cfg.UsePostgreSQL(AppConfig.ConnectionString, false, true));
            services.AddTransient<DownloadPhotoStep>();
            services.AddTransient<UploadImageStep>();
            services.AddTransient<GenerateVideoStep>();
            services.AddTransient<WaitForVideoStep>();
            services.AddTransient<NotifyUserStep>();
            services.AddSingleton<PhotoAnimationProgressNotifier>();
            services.AddSingleton<PhotoAnimationWorkflowService>();
            services.AddHostedService<WorkflowHostService>();

            services.AddSingleton<ISchedulerService, SchedulerService>();
            services.AddQuartz(q =>
            {
                q.UseMicrosoftDependencyInjectionJobFactory();
                q.UsePersistentStore(opt =>
                {
                    opt.UsePostgres(AppConfig.ConnectionString);
                    opt.UseNewtonsoftJsonSerializer();
                });
            });
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

            services.AddScoped<MainHandler>();
            services.AddScoped<ReceiverService>();
            services.AddHostedService<PollingService>();

            services
            // handlers
            .AddScoped<Dispatcher>()
            .AddScoped<RecoveryNotificationHandler>()
            .AddScoped<AdminCommandHandler>()
            .AddScoped<CommandHandler>()
            .AddScoped<ImageGeneratorHandler>()
            .AddScoped<ImageTextRecognitionHandler>()
            .AddScoped<ChatGptHandler>()
            // services
            .AddScoped<UserService>()
            .AddSingleton<TelejetAdClient>()
            .AddSingleton<IAdvertisementClient, GramadsAdvertisementClient>()
            .AddSingleton<ImageCreatorService>()
            .AddSingleton<IImageGenerator, YaCloudClient>()
            .AddSingleton<ITextRecognizer, YaCloudClient>()
            .AddScoped<UserStatusActivator>()
            .AddSingleton<SpeechToTextService>()
            .AddSingleton<RateLimiter>()
            .AddScoped<IGpt, ChatGptService>()
            .AddSingleton<TokenQueue>()
            .AddSingleton<IJobService, JobService>()
            .AddSingleton<YaPhotoAnimatorService>()
            .AddScoped<ChatGptService>()
            .AddScoped<OpenAiServiceCreator, ProxyApiService>()
            .AddScoped<ContextWindow>()
            .AddRepositories()
            .AddScoped<MoneyService>()
            .AddSingleton<ITelegramBotClient>(x =>
            {
                var botClient = ActivatorUtilities.CreateInstance<TelegramBotClient>(x, AppConfig.TelegramToken);

                CultureInfo.CurrentUICulture = LocalizationManager.Ru;
                InitializeBot(botClient, "ru");

                CultureInfo.CurrentUICulture = LocalizationManager.En;
                InitializeBot(botClient);

                return botClient;
            })
            .AddSingleton<InMemoryAdvertisementTracker>()
            ;

            services.AddDbContext<ApplicationContext>();

            services.AddHttpClient<IGpt, ChatGptService>(b =>
            {
                b.BaseAddress = new Uri("https://api.vsegpt.ru/v1/");
                b.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", AppConfig.ProxyApiApiKey);
            });

            services.AddImageCache();
            
            // dummy comment remove please
            return services;
        }

        private static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            return services.AddTransient<MessageRepository>()
                .AddScoped<UserRepository>()
                .AddScoped<UserCommandRepository>()
                .AddScoped<BotSettingsRepository>()
                .AddScoped<OpenaiAccountsRepository>()
                .AddScoped<WalletRepository>()
                .AddScoped<TransactionRepository>()
                .AddScoped<InvoiceRepository>();
        }

        static void InitializeBot(ITelegramBotClient botClient, string? langCode = null)
        {
            var botMenu = new BotMenu();
            botClient.SetMyCommands(botMenu.GetBotCommands(), languageCode: langCode);
            botClient.SetMyName(AppConfig.IsProduction ? BotResponse.BotName : BotResponse.DevBotName, languageCode: langCode);
            botClient.SetMyDescription(BotResponse.BotDescription, languageCode: langCode);
            botClient.SetMyShortDescription(AppConfig.IsProduction ? BotResponse.ShortDescription :
                BotResponse.ShortDevDescription, languageCode: langCode);
        }
    }
}
