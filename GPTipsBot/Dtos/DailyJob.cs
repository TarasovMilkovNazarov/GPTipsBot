using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace GPTipsBot.Dtos;

public class DailyJob : IJob
{
    private readonly ApplicationContext _context;

    public DailyJob(ApplicationContext context)
    {
        _context = context;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await UpdateUserDailyLimits();
        await RemoveOldMessages();
    }

    private async Task RemoveOldMessages()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        await _context.Messages
            .Where(m => m.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync();
    }

    private async Task UpdateUserDailyLimits()
    {
        await _context.Users
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FreeImageGenerations, AppConfig.FreeImageGenerationsCount)
                .SetProperty(u => u.FreeImageTextRecognitions, AppConfig.FreeImageGenerationsCount)
                .SetProperty(u => u.FreeGptRequests, AppConfig.FreeChatGptRequests)
            );
    }
}