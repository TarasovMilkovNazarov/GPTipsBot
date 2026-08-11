using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Models;
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
        var todayOffset = new DateTimeOffset(today, TimeSpan.Zero);

        var newUsersCount = await context.Users.CountAsync(u => u.CreatedAt > today);

        var counts = await context.Messages
            .Where(m => m.CreatedAt > today)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new StatisticsDto
            {
                ImagesCount = g.Count(m => m.Type == BotMessageType.ImageGenerated),
                RecognitionsCount = g.Count(m => m.Type == BotMessageType.RecognizeText),
                GptResponses = g.Count(m => m.Role == MessageOwner.Assistant),
                AnimatedPhotosCount = g.Count(m => m.Type == BotMessageType.AnimatedPhoto),
                PromptFromImageCount = g.Count(m => m.Type == BotMessageType.PromptFromImage)
            })
            .FirstOrDefaultAsync() ?? new();

        var mau = await CalculateMonthlyActiveUsers();

        var loginEvents = await context.AuthLoginEvents.AsNoTracking()
            .Where(e => e.CreatedAt > todayOffset)
            .Select(e => new { e.Provider, e.UserId })
            .ToListAsync();

        var loginStats = loginEvents
            .GroupBy(e => e.Provider)
            .ToDictionary(
                g => g.Key,
                g => (Count: g.Count(), UniqueUsers: g.Select(x => x.UserId).Distinct().Count()));

        static (int Count, int UniqueUsers) GetLoginStat(
            Dictionary<AuthProvider, (int Count, int UniqueUsers)> stats,
            AuthProvider provider) =>
            stats.TryGetValue(provider, out var value) ? value : (0, 0);

        var telegramLogins = GetLoginStat(loginStats, AuthProvider.Telegram);
        var guestLogins = GetLoginStat(loginStats, AuthProvider.Guest);
        var emailLogins = GetLoginStat(loginStats, AuthProvider.Email);
        var vkidLogins = GetLoginStat(loginStats, AuthProvider.Vkid);
        var yandexLogins = GetLoginStat(loginStats, AuthProvider.Yandex);
        var totalLogins = loginEvents.Count;

        var summaryUserIds = await context.UserCommands.AsNoTracking()
            .Where(c => c.CreatedAt > today && c.Type == CommandType.Summary)
            .Select(c => c.UserId)
            .ToListAsync();
        var summaryCommands = summaryUserIds.Count;
        var summaryUniqueUsers = summaryUserIds.Distinct().Count();

        var message = "#statistics" + Environment.NewLine +
                      $"New users created: {newUsersCount} for {today:dd.MM.yyyy}" + Environment.NewLine;
        message += Environment.NewLine + $"Images generated: {counts.ImagesCount}";
        message += Environment.NewLine + $"Animated photos count: {counts.AnimatedPhotosCount}";
        message += Environment.NewLine + $"Prompt from image: {counts.PromptFromImageCount}";
        message += Environment.NewLine + $"Text recognitions: {counts.RecognitionsCount}";
        message += Environment.NewLine + $"Gpt responses: {counts.GptResponses}";
        message += Environment.NewLine + $"/summary uses: {summaryCommands} ({summaryUniqueUsers} users)";
        message += Environment.NewLine + $"Web logins: {totalLogins}";
        message += Environment.NewLine + $"  telegram: {telegramLogins.Count} ({telegramLogins.UniqueUsers} users)";
        message += Environment.NewLine + $"  guest: {guestLogins.Count} ({guestLogins.UniqueUsers} users)";
        if (emailLogins.Count > 0 || vkidLogins.Count > 0 || yandexLogins.Count > 0)
        {
            message += Environment.NewLine + $"  email: {emailLogins.Count} ({emailLogins.UniqueUsers} users)";
            message += Environment.NewLine + $"  yandex: {yandexLogins.Count} ({yandexLogins.UniqueUsers} users)";
            message += Environment.NewLine + $"  vkid: {vkidLogins.Count} ({vkidLogins.UniqueUsers} users)";
        }
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