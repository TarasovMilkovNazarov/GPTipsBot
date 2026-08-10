using System.Collections.Concurrent;
using GPTipsBot.Db;
using GPTipsBot.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.Jobs;

public class BotUpdateInformerJob(ApplicationContext context, ITelegramBotClient botClient,
    ILogger<BotUpdateInformerJob> logger) : IJob
{
    private readonly ITelegramBotClient _botClient = botClient;

    public async Task Execute(IJobExecutionContext context1)
    {
        var botClient = new TelegramBotClient("");

        var message = BotResponse.AdvertisementText;

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
                try
                {
                    await botClient.SendMessage(user.TelegramId, message, ParseMode.MarkdownV2, cancellationToken: token);
                }
                catch (ApiRequestException ex) when (ex.ErrorCode is 403 or 400)
                {
                    kickedBotUserIds.Add(user.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error while sending advertisement for user: '{userId}'", user.Id);
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
