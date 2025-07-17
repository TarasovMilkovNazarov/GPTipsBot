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
        var imagesGeneratedCount = await _context.Messages.CountAsync(m =>
            m.Type == BotMessageType.ImageGenerated && m.CreatedAt > today);
        var chatGptMessagesCount = await _context.Messages.CountAsync(m =>
            m.Role == MessageOwner.Assistant && m.CreatedAt > today);

        var message = "#statistics" + Environment.NewLine +
                      $"New users created: {newUsersCount} for {today.ToShortDateString()}";
        message += Environment.NewLine + $"Images generated: {imagesGeneratedCount}";
        message += Environment.NewLine + $"Gpt requests: {chatGptMessagesCount}";

        await _botClient.SendTextMessageAsync(AppConfig.AdminIds.First(), message);
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