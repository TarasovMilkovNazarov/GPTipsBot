using GPTipsBot;
using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;

// ReSharper disable once CheckNamespace
namespace Telegram.Bot.Abstract;

/// <summary>
/// An abstract class to compose Receiver Service and Update Handler classes
/// </summary>
/// <typeparam name="TUpdateHandler">Update Handler to use in Update Receiver</typeparam>
public abstract class ReceiverServiceBase<TUpdateHandler> : IReceiverService
{
    private readonly ITelegramBotClient botClient;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<ReceiverServiceBase<TUpdateHandler>> log;

    internal ReceiverServiceBase(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<ReceiverServiceBase<TUpdateHandler>> log)
    {
        this.botClient = botClient;
        this.serviceProvider = serviceProvider;
        this.log = log;
    }

    /// <summary>
    /// Start to service Updates with provided Update Handler class
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    public async Task ReceiveAsync(CancellationToken stoppingToken)
    {
        await Init(stoppingToken);

        var tasks = new List<Task>();
        try
        {
            // Start receiving updates
            var updateReceiver = new QueuedUpdateReceiver(botClient, new ReceiverOptions(), PollingErrorHandler);
            await foreach (var update in updateReceiver.WithCancellation(stoppingToken))
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var scope = serviceProvider.CreateScope();
                    using (log.BeginScope(new { msgId = update.Id }))
                    {
                        log.LogInformation("Handling message '{text}' with id={updateId} from {userName}(id={userId}) in chat {chatId}",
                            update.Message?.Text, update.Id, update.Message?.From?.Username, update.Message?.From?.Id, update.Message?.Chat.Id);
                        try
                        {
                            var worker = scope.ServiceProvider.GetRequiredService<UpdateHandlerEntryPoint>();
                            await worker.HandleUpdateAsync(update);
                        }
                        catch (ApiRequestException e)
                        {
                            log.LogError(e, "Telegram API Error [{Code}] {Message}", e.ErrorCode, e.Message);
                        }
                        catch (Exception e)
                        {
                            log.LogError(e, "Error while handling update");
                            if (update.Message == null)
                                return;

                            await botClient.SendTextMessageAsync(update.Message.Chat!.Id, BotResponse.SomethingWentWrong,
                                cancellationToken: stoppingToken);
                        }
                    }
                }, stoppingToken));
            }
        }
        catch (OperationCanceledException)
        {
            log.LogInformation("Update receiving operation was canceled");
        }
        catch (Exception e)
        {
            log.LogCritical(e, "Пипец упалось всё! Получение сообщений от телеграмма остановленно. Завершаем работу приложения. " +
                               "Сюда мы не должны попадать! Такое исключение надо ловить и обрабатывать выше по стеку");
        }
        finally
        {
            await WaitForUnfinishedTasks(tasks, TimeSpan.FromMinutes(1));
        }
    }

    private async Task Init(CancellationToken stoppingToken)
    {
        var me = await botClient.GetMeAsync(stoppingToken);
        AppConfig.BotName = me.Username ?? "GPTipsBot";
        log.LogInformation("Bot running. {BotName} is ready to receive messages", AppConfig.BotName);
        _ = botClient.SendBotVersionAsync(AppConfig.AdminIds);
    }

    private async Task WaitForUnfinishedTasks(List<Task> tasks, TimeSpan timeout)
    {
        var unfinished = tasks.Where(t => t is { IsCanceled: false, IsCompleted: false, IsFaulted: false });

        if (!unfinished.Any())
            return;

        log.LogInformation("Wait for {UnfinishedRequestCount} unfinished request from users", unfinished);

        var timeoutTask = TimeoutTask(timeout);
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

        if (completedTask == timeoutTask)
            log.LogWarning("Timeout has expired. Not all tasks have been completed.");
        else
            log.LogInformation("All tasks have been completed.");
    }

    private static Task TimeoutTask(TimeSpan timeout)
    {
#pragma warning disable CA2016
        // ReSharper disable once MethodSupportsCancellation
        var timeoutTask = Task.Delay(timeout);
#pragma warning restore CA2016
        return timeoutTask;
    }

    private Task PollingErrorHandler(Exception e, CancellationToken arg2)
    {
        log.LogError(e, "Update receiving operation has error");
        return Task.CompletedTask;
    }
}