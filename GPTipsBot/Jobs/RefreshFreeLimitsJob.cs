using GPTipsBot.Config;
using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace GPTipsBot.Jobs;

public class RefreshFreeLimitsJob: IJob
{
    private readonly ApplicationContext _context;

    public RefreshFreeLimitsJob(ApplicationContext context)
    {
        _context = context;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _context.Users
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FreeImageGenerations, PaymentConfig.FreeImageGenerations)
                .SetProperty(u => u.FreeImageTextRecognitions, PaymentConfig.FreeTextRecognitions)
                .SetProperty(u => u.FreeGptRequests, PaymentConfig.FreeChatGptRequests)
            );
    }
}