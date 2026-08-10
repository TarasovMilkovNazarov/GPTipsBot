using System.Collections.Concurrent;
using GPTipsBot.Config;
using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Jobs;

/// <summary>
/// Определяет покинул ли юзер чат чат (сетит IsActive=false)
/// </summary>
public class DeactivateKickedUsersJob(
    ApplicationContext context,
    ITelegramBotClient botClient,
    ILogger<DeactivateKickedUsersJob> logger)
    : IJob
{
    public async Task Execute(IJobExecutionContext context1)
    {
        var today = DateTime.UtcNow.Date;

        await SoftlyRemoveBlockedUsers();
        var activeUsersAfter = await context.Users.CountAsync(u => u.IsActive);

        var message = "#active_users" + Environment.NewLine +
                      $"Count: {activeUsersAfter}";

        await botClient.SendMessage(AppConfig.AdminIds.First(), message);
    }

    private async Task SoftlyRemoveBlockedUsers()
    {
        const int batchSize = 100;
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
        var kickedBotUserIds = new ConcurrentBag<long>();

        for (var skip = 0; ; skip += batchSize)
        {
            var userBatch = await context.Users
                .Where(u => u.IsActive && u.TelegramId != null)
                .OrderBy(u => u.Id)
                .Select(u => new { u.Id, TelegramId = u.TelegramId!.Value })
                .Skip(skip)
                .Take(batchSize)
                .ToListAsync();

            if (!userBatch.Any())
                break;

            await Parallel.ForEachAsync(userBatch, parallelOptions, async (user, token) =>
            {
                var cts = new CancellationTokenSource();
                try
                {
                    await botClient.SendChatAction(user.TelegramId, ChatAction.Typing, cancellationToken: cts.Token);
                }
                catch (ApiRequestException ex) when (ex.ErrorCode is 403 or 400)
                {
                    kickedBotUserIds.Add(user.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error while performing of deactivation user: '{userId}'", user.Id);
                }
                finally
                {
                    await cts.CancelAsync();
                }
            });

            if (kickedBotUserIds.Any())
            {
                await context.Users
                    .Where(u => kickedBotUserIds.Contains(u.Id) && u.IsActive)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));

                kickedBotUserIds.Clear();
            }
        }
    }
}
