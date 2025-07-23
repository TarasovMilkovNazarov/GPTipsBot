using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace GPTipsBot.Jobs;

public class RemoveOldRecordsJob : IJob
{
    private readonly ApplicationContext _context;

    public RemoveOldRecordsJob(ApplicationContext context)
    {
        _context = context;
    }

    public async Task Execute(IJobExecutionContext context)
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
}