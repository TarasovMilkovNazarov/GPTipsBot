using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace GPTipsBot.Jobs;

public class RemoveOldRecordsJob(ApplicationContext context) : IJob
{
    public async Task Execute(IJobExecutionContext context1)
    {
        const int batchSize = 100;

        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        while (true)
        {
            var deleteCount = await context.Messages
                .Where(m => m.CreatedAt < cutoffDate)
                .Take(batchSize)
                .ExecuteDeleteAsync();

            if (deleteCount == 0) break;
        }
    }
}