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
        AppConfig.BotName = me.Username ?? "GPTipsBot";
        log.LogInformation("Start receiving updates for {BotName}", AppConfig.BotName);

        try
        { 
            // Start receiving updates
            var updateReceiver = new QueuedUpdateReceiver(botClient, new ReceiverOptions(), PollingErrorHandler);
            await foreach (var update in updateReceiver.WithCancellation(stoppingToken))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        using (ForContext(update))
                        {
                            var worker = scope.ServiceProvider.GetRequiredService<UpdateHandlerEntryPoint>();
                            await worker.HandleUpdateAsync(botClient, update);
                        }
                    }
                    catch (Exception e)
                    {
                        log.LogError(e, "Ошибка при обработке запроса");
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

    private IDisposable? ForContext(Update update)
    {
        var scope = log.BeginScope("Handling message with id={updateId} from {userName}(id={userId}) in chat {chatId}",
            update.Id, update.Message?.From?.Username, update.Message?.From?.Id, update.Message?.Chat?.Id);
        log.LogInformation("Handling message with id={updateId} from {userName}(id={userId}) in chat {chatId}",
            update.Id, update.Message?.From?.Username, update.Message?.From?.Id, update.Message?.Chat?.Id);
        return scope;
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