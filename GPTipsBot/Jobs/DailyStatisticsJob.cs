using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Telegram.Bot;

namespace GPTipsBot.Jobs;

public class DailyStatisticsJob: IJob
{
    private readonly ApplicationContext _context;
    private readonly ITelegramBotClient _botClient;

    public DailyStatisticsJob(ApplicationContext context, ITelegramBotClient botClient)
    {
        _context = context;
        _botClient = botClient;
    }

    public async Task Execute(IJobExecutionContext context)
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
}