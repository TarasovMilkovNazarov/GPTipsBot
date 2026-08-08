using GPTipsBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace GPTipsBot.Jobs;

public class ReleaseExpiredPaymentHoldsJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ReleaseExpiredPaymentHoldsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var userService = scope.ServiceProvider.GetRequiredService<UserService>();
            await userService.ReleaseExpiredHoldsAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ReleaseExpiredPaymentHoldsJob failed");
        }
    }
}
