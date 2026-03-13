using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;

namespace GPTipsBot.Jobs;

public class DailyStatisticsJob(
    ApplicationContext context,
    ITelegramBotClient botClient,
    ILogger<DailyStatisticsJob> logger)
    : IJob
{
    private readonly ILogger<DailyStatisticsJob> _logger = logger;

    public async Task Execute(IJobExecutionContext context1)
    {
        var today = DateTime.UtcNow.Date;

        var newUsersCount = await context.Users.CountAsync(u => u.CreatedAt > today);

        var counts = await context.Messages
            .Where(m => m.CreatedAt > today)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new StatisticsDto
            {
                ImagesCount = g.Count(m => m.Type == BotMessageType.ImageGenerated),
                RecognitionsCount = g.Count(m => m.Type == BotMessageType.RecognizeText),
                GptResponses = g.Count(m => m.Role == MessageOwner.Assistant),
                AnimatedPhotosCount = g.Count(m => m.Type == BotMessageType.AnimatedPhoto)
            })
            .FirstOrDefaultAsync() ?? new();

        var mau = await CalculateMonthlyActiveUsers();

        var message = "#statistics" + Environment.NewLine +
                      $"New users created: {newUsersCount} for {today:dd.MM.yyyy}" + Environment.NewLine;
        message += Environment.NewLine + $"Images generated: {counts.ImagesCount}";
        message += Environment.NewLine + $"Animated photos count: {counts.AnimatedPhotosCount}";
        message += Environment.NewLine + $"Text recognitions: {counts.RecognitionsCount}";
        message += Environment.NewLine + $"Gpt responses: {counts.GptResponses}";
        message += Environment.NewLine + $"Monthly users: {mau}";

        await botClient.SendMessage(AppConfig.AdminIds.First(), message);
    }

    private async Task<long> CalculateMonthlyActiveUsers()
    {
        var currentMonth = DateTime.Now.Month;

        var mau = await context.UserCommands.AsNoTracking()
            .Where(c => c.CreatedAt.Month == currentMonth).GroupBy(c => c.UserId).CountAsync();

        return mau;
    }
}