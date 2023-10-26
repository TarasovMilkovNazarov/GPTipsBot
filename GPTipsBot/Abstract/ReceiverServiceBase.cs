using GPTipsBot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

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
        var tasks = new List<Task>();
        var me = await botClient.GetMeAsync(stoppingToken);
        log.LogInformation("Start receiving updates for {BotName}", me.Username ?? "GPTipsBot");

        
        try
        { 
            // Start receiving updates
            var updateReceiver = new QueuedUpdateReceiver(botClient, new ReceiverOptions(), PollingErrorHandler);
            await foreach (var update in updateReceiver.WithCancellation(stoppingToken))
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var scope = serviceProvider.CreateScope();
                    using (ForContext(update))
                    {
                        var worker = scope.ServiceProvider.GetRequiredService<UpdateHandlerEntryPoint>();
                        await worker.HandleUpdateAsync(botClient, update);
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
            log.LogCritical(e, "Пипец упалось всё! Получение сообщений от телеграмма остановленно. " +
                                  "Сюда мы не должны попадать! Такое исключение надо ловить и обрабатывать выше по стеку");
        }
        finally
        {
            await WaitForUnfinishedTasks(tasks, TimeSpan.FromMinutes(1));
        }
    }

    private IDisposable? ForContext(Update update)
    {
        return log.BeginScope("Handling message with {updateId} from {userName}({userId})",
            update.Id, update.Message?.From?.Username, update.Message?.From?.Id);
    }
    
    private async Task WaitForUnfinishedTasks(List<Task> tasks, TimeSpan timeout)
    {
        log.LogInformation("Wait for {UnfinishedRequestCount} unfinished request from users", tasks.Count);

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