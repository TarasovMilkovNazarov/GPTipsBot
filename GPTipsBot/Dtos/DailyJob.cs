using GPTipsBot.Db;
using GPTipsBot.Enums;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Telegram.Bot;

namespace GPTipsBot.Dtos;

public class DailyJob : IJob
{
    private readonly ApplicationContext _context;
    private readonly ITelegramBotClient _botClient;

    public DailyJob(ApplicationContext context, ITelegramBotClient botClient)
    {
        _context = context;
        _botClient = botClient;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await SendStatistics();
        await UpdateUserDailyLimits();
        await RemoveOldMessages();
    }

    private async Task SendStatistics()
    {
        var today = DateTime.UtcNow.Date;

        var newUsersCount = await _context.Users.CountAsync(u => u.CreatedAt > today);

        var counts = await _context.Messages
            .Where(m => m.CreatedAt > today)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new StatisticsDto
            {
                ImagesCount = g.Count(m => m.Type == BotMessageType.ImageGenerated),
                RecognitionsCount = g.Count(m => m.Type == BotMessageType.RecognizeText),
                GptResponses = g.Count(m => m.Role == MessageOwner.Assistant)
            })
            .FirstOrDefaultAsync() ?? new();

        var message = "#statistics" + Environment.NewLine +
                      $"New users created: {newUsersCount} for {today:dd.MM.yyyy}" + Environment.NewLine;
        message += Environment.NewLine + $"Images generated: {counts.ImagesCount}";
        message += Environment.NewLine + $"Text recognitions: {counts.RecognitionsCount}";
        message += Environment.NewLine + $"Gpt responses: {counts.GptResponses}";

        await _botClient.SendTextMessageAsync(AppConfig.AdminIds.First(), message);
    }

    private async Task RemoveOldMessages()
    {
        const int batchSize = 100;

        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        while (true)
        {
            var deleteCount = await _context.Messages
                .Where(m => m.CreatedAt < cutoffDate)
                .Take(batchSize)
                .ExecuteDeleteAsync();

            if (deleteCount == 0) break;
        }
    }

    private async Task UpdateUserDailyLimits()
    {
        await _context.Users
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FreeImageGenerations, PaymentConstants.FreeImageGenerationsCount)
                .SetProperty(u => u.FreeImageTextRecognitions, PaymentConstants.FreeImageGenerationsCount)
                .SetProperty(u => u.FreeGptRequests, PaymentConstants.FreeChatGptRequests)
            );
    }
}