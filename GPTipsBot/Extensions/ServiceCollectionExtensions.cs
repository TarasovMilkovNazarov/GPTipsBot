using GPTipsBot.Services.YooKassa;
using GPTipsBot.Db;
using GPTipsBot.Localization;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using GPTipsBot.Config;
using GPTipsBot.Jobs;
using Telegram.Bot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Resources;
using GPTipsBot.Services.Cache;
using GPTipsBot.Services.Broadcast;
using GPTipsBot.Services.Inline;
using GPTipsBot.Services.YandexCloud;
using GPTipsBot.Services.YandexCloud.Workflow;
using GPTipsBot.Services.YandexCloud.Workflow.Steps;
using GPTipsBot.Services.YandexPhotoAnimator;
using GPTipsBot.Services.YandexPhotoAnimator.Workflow;
using PhotoAnimationSteps = GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;
using WorkflowCore.Persistence.PostgreSQL;
using Quartz;

namespace GPTipsBot.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            services.AddOpenAiClient();
            services.AddWorkflow(cfg => cfg.UsePostgreSQL(AppConfig.ConnectionString, false, true));
            services.AddTransient<PhotoAnimationSteps.DownloadPhotoStep>();
            services.AddTransient<PhotoAnimationSteps.UploadImageStep>();
            services.AddTransient<PhotoAnimationSteps.GenerateVideoStep>();
            services.AddTransient<PhotoAnimationSteps.WaitForVideoStep>();
            services.AddTransient<PhotoAnimationSteps.NotifyUserStep>();
            services.AddTransient<PhotoAnimationSteps.DownloadAliceImagesStep>();
            services.AddTransient<PhotoAnimationSteps.UploadAliceImagesStep>();
            services.AddTransient<PhotoAnimationSteps.GenerateAliceImageStep>();
            services.AddTransient<PhotoAnimationSteps.WaitForAliceImageStep>();
            services.AddTransient<PhotoAnimationSteps.NotifyAliceImageStep>();
            services.AddTransient<GenerateImageStep>();
#pragma warning disable CS0618 // kept registered for workflow instances started before the sync switch
            services.AddTransient<StartGenerateImageStep>();
            services.AddTransient<WaitForImageStep>();
#pragma warning restore CS0618
            services.AddTransient<NotifyUserStep>();
            services.AddSingleton<PhotoAnimationProgressNotifier>();
            services.AddSingleton<PhotoAnimationWorkflowService>();
            services.AddSingleton<AliceImageWorkflowService>();
            services.AddSingleton<ImageGenerationWorkflowService>();
            services.AddHostedService<WorkflowHostService>();

            services.AddSingleton<ISchedulerService, SchedulerService>();
            services.AddTransient<DailyStatisticsJob>();
            services.AddTransient<RefreshFreeLimitsJob>();
            services.AddTransient<RemoveOldRecordsJob>();
            services.AddTransient<DeactivateKickedUsersJob>();
            services.AddTransient<SyncYooKassaPaymentsJob>();
            services.AddTransient<ReleaseExpiredPaymentHoldsJob>();
            services.AddTransient<BotUpdateInformerJob>();
            services.AddTransient<YandexCloudBalanceAlertJob>();
            services.AddSingleton<YandexBillingAlertState>();
            services.AddQuartz(q =>
            {
                q.UsePersistentStore(opt =>
                {
                    opt.UsePostgres(AppConfig.ConnectionString);
                    opt.UseNewtonsoftJsonSerializer();
                });
            });
            services.AddQuartzHostedService(options =>
            {
                options.WaitForJobsToComplete = true;
            });
            services.AddMemoryCache();
            services.AddLogging();
            services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
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
            .AddScoped<PromptFromImageHandler>()
            .AddScoped<ChatGptHandler>()
            .AddScoped<GptImageHandler>()
            .AddScoped<StickerPackHandler>()
            .AddScoped<RemoveWatermarkHandler>()
            .AddScoped<InlineQueryHandler>()
            // services
            .AddScoped<UserService>()
            .AddSingleton<TelejetAdClient>()
            .AddSingleton<IAdvertisementClient, GramadsAdvertisementClient>()
            .AddSingleton<ImageCreatorService>()
            .AddSingleton<StickerPackService>()
            .AddSingleton<IGptImageSessionCache, GptImageSessionCache>()
            .AddSingleton<IStickerPackSessionCache, StickerPackSessionCache>()
            .AddSingleton<IImageGenerator, YaCloudClient>()
            .AddSingleton<ITextRecognizer, YaCloudClient>()
            .AddScoped<UserStatusActivator>()
            .AddSingleton<SpeechToTextService>()
            .AddSingleton<RateLimiter>()
            .AddSingleton<BroadcastDraftStore>()
            .AddSingleton<BroadcastRunner>()
            .AddHostedService(sp => sp.GetRequiredService<BroadcastRunner>())
            .AddScoped<BroadcastService>()
            .AddScoped<InferMissingUserLanguages>()
            // .AddHostedService<InferMissingUserLanguagesService>()
            .AddSingleton<InlinePendingStore>()
            .AddSingleton<AccountLinkTokenService>()
            .AddScoped<IGpt, ChatGptService>()
            .AddScoped<ChatToolExecutor>()
            .AddScoped<MediaIntentClassifier>()
            .AddSingleton<OpenAiVpnConnectivityService>()
            .AddSingleton<IJobService, JobService>()
            .AddSingleton<YaPhotoAnimatorService>()
            .AddScoped<ChatGptService>()
            .AddScoped<ContextWindow>()
            .AddRepositories()
            .AddScoped<MoneyService>()
            .AddScoped<GPTipsBot.Services.Email.SmtpEmailSender>()
            .AddScoped<GPTipsBot.Web.WebUserService>()
            .AddScoped<GPTipsBot.Web.WebChatService>()
            .AddScoped<GPTipsBot.Web.WebMediaService>()
            .AddSingleton<ITelegramBotClient>(_ =>
            {
                // Отдельный HttpClient: HappHttpClient OpenAI мутирует BaseAddress/headers.
                var httpClient = new HttpClient(new HappProxyClientHandler());
                var botClient = new TelegramBotClient(AppConfig.TelegramToken, httpClient);

                CultureInfo.CurrentUICulture = LocalizationManager.Ru;
                SetBotMenus(botClient, "ru");

                CultureInfo.CurrentUICulture = LocalizationManager.En;
                SetBotMenus(botClient);

                CultureInfo.CurrentUICulture = LocalizationManager.Es;
                SetBotMenus(botClient, "es");

                CultureInfo.CurrentUICulture = LocalizationManager.Fa;
                SetBotMenus(botClient, "fa");

                CultureInfo.CurrentUICulture = LocalizationManager.Ar;
                SetBotMenus(botClient, "ar");

                CultureInfo.CurrentUICulture = LocalizationManager.Ru;
                CultureInfo.DefaultThreadCurrentUICulture = LocalizationManager.Ru;

                return botClient;
            })
            .AddSingleton<BotCommandMenuService>()
            .AddSingleton<InMemoryAdvertisementTracker>()
            ;

            services.AddDbContext<ApplicationContext>();

            services.AddHttpClient<YandexBillingAccountClient>();
            services.AddHttpClient<GPTipsBot.Web.YandexOAuthClient>();
            services.AddHttpClient<YooKassaClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.yookassa.ru/v3/");
            });
            services.AddHttpClient<GPTipsBot.Services.OpenRouter.OpenRouterImageClient>(client =>
            {
                client.BaseAddress = new Uri(WatermarkRemovalConfig.BaseUrl);
                client.Timeout = WatermarkRemovalConfig.RequestTimeout;
            }).ConfigurePrimaryHttpMessageHandler(() => new HappProxyClientHandler());

            services.AddImageCache();
            services.AddVisionImageCache();
            services.AddAliceImageSessionCache();
            
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

        static void SetBotMenus(ITelegramBotClient botClient, string? langCode = null)
        {
            var botMenu = new BotMenu();
            var privateCommands = botMenu.GetBotCommands();
            var groupCommands = botMenu.GetGroupBotCommands();

            botClient.SetMyCommands(privateCommands, languageCode: langCode);
            botClient.SetMyCommands(privateCommands, BotCommandScope.AllPrivateChats(), languageCode: langCode);
            botClient.SetMyCommands(groupCommands, BotCommandScope.AllGroupChats(), languageCode: langCode);
            botClient.SetMyCommands(groupCommands, BotCommandScope.AllChatAdministrators(), languageCode: langCode);

            botClient.SetMyName(AppConfig.IsProduction ? BotResponse.BotName : BotResponse.DevBotName, languageCode: langCode);
            botClient.SetMyDescription(BotResponse.BotDescription, languageCode: langCode);
            botClient.SetMyShortDescription(AppConfig.IsProduction ? BotResponse.ShortDescription :
                BotResponse.ShortDevDescription, languageCode: langCode);
        }
    }
}
