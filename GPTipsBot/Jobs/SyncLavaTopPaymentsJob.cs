using GPTipsBot.Config;
using GPTipsBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace GPTipsBot.Jobs;

/// <summary>
/// Polls lava.top for Created invoices when HTTP notifications are delayed or missed.
/// </summary>
public class SyncLavaTopPaymentsJob(
    IServiceScopeFactory scopeFactory,
    ILogger<SyncLavaTopPaymentsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!LavaTopConfig.IsEnabled)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var moneyService = scope.ServiceProvider.GetRequiredService<MoneyService>();
            var credited = await moneyService.SyncPendingLavaTopPaymentsAsync(context.CancellationToken);
            if (credited > 0)
            {
                logger.LogInformation("SyncLavaTopPaymentsJob credited/confirmed {Count} payment(s)", credited);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SyncLavaTopPaymentsJob failed");
        }
    }
}
