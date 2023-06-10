﻿using GPTipsBot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GPTipsBot.Db;
using Microsoft.Extensions.Configuration;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using OpenAI.Extensions;
using GPTipsBot.Api;
using Telegram.Bot;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Services;
using Telegram.Bot.Polling;
using dotenv.net;

DotEnv.Fluent().WithProbeForEnv(10).Load();

var host = Host.CreateDefaultBuilder(args)
        // Add configuration, logging, ...
    .ConfigureServices((hostContext, services) =>
    {
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
        .AddTransient<ImageService>()
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
        .AddScoped(x => ActivatorUtilities.CreateInstance<TelegramBotAPI>(x, AppConfig.TelegramToken, AppConfig.СhatId))
        .AddScoped<MessageRepository>()
        .AddScoped<UserRepository>()
        .AddScoped<ITelegramBotClient, TelegramBotClient>(x => ActivatorUtilities.CreateInstance<TelegramBotClient>(x, AppConfig.TelegramToken));

        services.AddOpenAIService(settings => { settings.ApiKey = AppConfig.OpenAiToken; });
    }).Build();

await host.RunAsync();