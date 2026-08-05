using GPTipsBot.Config;
using GPTipsBot.Db;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace GPTipsBot.Jobs;

public class RefreshFreeLimitsJob(ApplicationContext context) : IJob
{
    public async Task Execute(IJobExecutionContext context1)
    {
        await context.Users
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FreeImageGenerations, PaymentConfig.FreeImageGenerations)
                .SetProperty(u => u.FreeImageTextRecognitions, PaymentConfig.FreeTextRecognitions)
                .SetProperty(u => u.FreeGptRequests, PaymentConfig.FreeChatGptRequests)
                .SetProperty(u => u.FreePhotoAnimations, PaymentConfig.FreePhotoAnimations)
            );
    }
}