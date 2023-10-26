﻿using GPTipsBot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace Telegram.Bot.Abstract;

/// <summary>
/// An abstract class to compose Receiver Service and Update Handler classes
/// </summary>
/// <typeparam name="TUpdateHandler">Update Handler to use in Update Receiver</typeparam>
public abstract class ReceiverServiceBase<TUpdateHandler> : IReceiverService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReceiverServiceBase<TUpdateHandler>> _logger;

    internal ReceiverServiceBase(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<ReceiverServiceBase<TUpdateHandler>> logger)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Start to service Updates with provided Update Handler class
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    public async Task ReceiveAsync(CancellationToken stoppingToken)
    {
        // ToDo: we can inject ReceiverOptions through IOptions container
        var receiverOptions = new ReceiverOptions()
        {
            //AllowedUpdates = new UpdateType[] { 
            //    UpdateType.Message, 
            //    UpdateType.InlineQuery, 
            //    UpdateType.CallbackQuery, 
            //    UpdateType.ChatMember },
            AllowedUpdates = Array.Empty<UpdateType>(),
        };

        var me = await _botClient.GetMeAsync(stoppingToken);
        _logger.LogInformation("Start receiving updates for {BotName}", me.Username ?? "GPTipsBot");

        try
        {
            var updateReceiver = new QueuedUpdateReceiver(_botClient, receiverOptions, PollingErrorHandler); // Start receiving updates
            await foreach (var update in updateReceiver.WithCancellation(stoppingToken))
            {
                var worker = _serviceProvider.GetRequiredService<UpdateHandlerEntryPoint>();
                _ = worker.HandleUpdateAsync(_botClient, update);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update receiving operation was canceled");
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Пипец упалось всё! Получение сообщений от телеграмма остановленно" +
                                   "Сюда мы не должны попадать! Такое исключение надо ловить и обрабатывать выше по стеку");
        }
    }

    private Task PollingErrorHandler(Exception e, CancellationToken arg2)
    {
        _logger.LogError(e, "Update receiving operation has error");
        return Task.CompletedTask;
    }
}