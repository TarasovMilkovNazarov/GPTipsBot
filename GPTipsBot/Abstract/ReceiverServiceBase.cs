using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPTipsBot;
using GPTipsBot.Config;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

// ReSharper disable once CheckNamespace
namespace Telegram.Bot.Abstract;

/// <summary>
/// An abstract class to compose Receiver Service and Update Handler classes
/// </summary>
/// <typeparam name="TUpdateHandler">Update Handler to use in Update Receiver</typeparam>
public abstract class ReceiverServiceBase<TUpdateHandler> : IReceiverService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReceiverServiceBase<TUpdateHandler>> _log;

    /// <summary>Repeat polling failures are logged at most this often while an outage lasts.</summary>
    private static readonly TimeSpan PollingErrorLogInterval = TimeSpan.FromMinutes(1);

    /// <summary>A quiet period longer than this starts counting a new outage from scratch.</summary>
    private static readonly TimeSpan PollingIncidentGap = TimeSpan.FromMinutes(2);

    private DateTime _lastPollingFailureAt = DateTime.MinValue;
    private DateTime _lastPollingErrorLoggedAt = DateTime.MinValue;
    private int _consecutivePollingFailures;

    internal ReceiverServiceBase(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<ReceiverServiceBase<TUpdateHandler>> log)
    {
        _botClient = botClient;
        _serviceProvider = serviceProvider;
        _log = log;
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
            var updateReceiver = new QueuedUpdateReceiver(_botClient, new ReceiverOptions(), PollingErrorHandler);
            await foreach (var update in updateReceiver.WithCancellation(stoppingToken))
            {
                tasks.Add(Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var user = update.GetUser();

                    if (user == null)
                    {
                        return;
                    }

                    if (user.Id is 1863372695 or 5831793328)
                    {
                        var updateStr = SerializeUpdate(update);
                        _log.LogWarning("Suspicious update" +
                                        Environment.NewLine + "{update}", updateStr);
                    }

                    var userId = user.Id;
                    var chatId = update.GetChatId();
                    using var cultureScope = UiCultureScope.ForLanguage(update.GetLanguageOrDefault());
                    using (_log.BeginScope(new [] {update.Id, userId}))
                    {
                        _log.LogInformation("Handling message '{text}' with id={updateId} from {userName}(id={userId}) in chat {chatId}",
                            update.Message?.Text, update.Id, user?.Username, userId, chatId);
                        try
                        {
                            var worker = scope.ServiceProvider.GetRequiredService<MainHandler>();
                            await worker.HandleUpdateAsync(update);
                        }
                        catch (IgnoreMessageTypeException e)
                        {
                            // ignore
                        }
                        catch (ClientException clientEx)
                        {
                            await _botClient.SendMessageWithMenuAsync(
                                clientEx.ChatId, clientEx.Message, cancellationToken: stoppingToken);
                        }
                        catch (ClientCanceledException clientCanceledException)
                        {
                            await _botClient.SendMessageWithMenuAsync(
                                clientCanceledException.ChatId,
                                clientCanceledException.Message,
                                TelegramBotUiService.CancelInlineKeyboard,
                                cancellationToken: stoppingToken);
                        }
                        catch (NotSupportedMessageException notSupportedMessageEx)
                        {
                            await _botClient.SendMessageWithMenuAsync(
                                notSupportedMessageEx.ChatId,
                                BotResponse.UnsupportedMessageType,
                                cancellationToken: stoppingToken);
                        }
                        catch (ApiRequestException e)
                        {
                            if (e.ErrorCode == 403)
                            {
                                var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();
                                await userRepository.SoftlyRemoveUser(userId);
                            }
                            else if (IsBenignEditError(e))
                            {
                                _log.LogDebug("Telegram edit no-op [{Code}] {Message}", e.ErrorCode, e.Message);
                            }
                            else
                            {
                                _log.LogError(e, "Telegram API Error [{Code}] {Message}", e.ErrorCode, e.Message);
                            }
                        }
                        catch (Exception e)
                        {
                            var updateStr = SerializeUpdate(update);

                            _log.LogError(e, "Unknown error while handling update" +
                                             Environment.NewLine + "{update}", updateStr);

                            await _botClient.SendMessageWithMenuAsync(
                                chatId ?? userId,
                                BotResponse.SomethingWentWrong,
                                cancellationToken: stoppingToken);
                        }
                    }
                }, stoppingToken));
            }
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("Update receiving operation was canceled");
        }
        catch (Exception e)
        {
            _log.LogCritical(e, "Пипец упалось всё! Получение сообщений от телеграмма остановленно. Завершаем работу приложения. " +
                               "Сюда мы не должны попадать! Такое исключение надо ловить и обрабатывать выше по стеку");
        }
        finally
        {
            await WaitForUnfinishedTasks(tasks, TimeSpan.FromMinutes(1));
        }
    }

    private static string SerializeUpdate(Update update)
    {
        var updateStr = System.Text.Json.JsonSerializer.Serialize(update,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        return updateStr;
    }

    private async Task Init(CancellationToken stoppingToken)
    {
        var me = await _botClient.GetMe(stoppingToken);
        AppConfig.BotName = me.Username ?? "GPTipsBot";
        _log.LogInformation("Bot running. {BotName} is ready to receive messages", AppConfig.BotName);
        _ = _botClient.SendBotVersionAsync(AppConfig.AdminIds);
    }

    private async Task WaitForUnfinishedTasks(List<Task> tasks, TimeSpan timeout)
    {
        var unfinished = tasks.Where(t => t is { IsCanceled: false, IsCompleted: false, IsFaulted: false });

        if (!unfinished.Any())
            return;

        _log.LogInformation("Wait for {UnfinishedRequestCount} unfinished request from users", unfinished);

        var timeoutTask = TimeoutTask(timeout);
        var completedTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

        if (completedTask == timeoutTask)
            _log.LogWarning("Timeout has expired. Not all tasks have been completed.");
        else
            _log.LogInformation("All tasks have been completed.");
    }

    private static Task TimeoutTask(TimeSpan timeout)
    {
#pragma warning disable CA2016
        // ReSharper disable once MethodSupportsCancellation
        var timeoutTask = Task.Delay(timeout);
#pragma warning restore CA2016
        return timeoutTask;
    }

    private async Task PollingErrorHandler(Exception e, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // A gap without failures means the previous outage is over and this is a fresh incident.
        if (now - _lastPollingFailureAt > PollingIncidentGap)
        {
            _consecutivePollingFailures = 0;
        }

        _lastPollingFailureAt = now;
        _consecutivePollingFailures++;

        if (_consecutivePollingFailures == 1 || now - _lastPollingErrorLoggedAt >= PollingErrorLogInterval)
        {
            _lastPollingErrorLoggedAt = now;
            _log.LogError(e, "Update receiving operation has error ({FailureCount} consecutive failures)",
                _consecutivePollingFailures);
        }

        // When Telegram is unreachable the receiver retries immediately, so one outage used to
        // produce hundreds of thousands of identical error logs. Back off instead of hot-looping.
        var backoffSeconds = Math.Min(30d, Math.Pow(2, Math.Min(_consecutivePollingFailures, 5)));
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>
    /// 400s that mean "the message is already in the state we wanted" — harmless, and they used to
    /// be the single noisiest error in the logs.
    /// </summary>
    private static bool IsBenignEditError(ApiRequestException e) =>
        e.ErrorCode == 400 &&
        (e.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase) ||
         e.Message.Contains("there is no text in the message to edit", StringComparison.OrdinalIgnoreCase) ||
         e.Message.Contains("message to edit not found", StringComparison.OrdinalIgnoreCase));
}