using GPTipsBot.Config;
using GPTipsBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace GPTipsBot.Jobs;

/// <summary>
/// Polls YooKassa for Created invoices when HTTP notifications are delayed or missed.
/// </summary>
public class SyncYooKassaPaymentsJob(
    IServiceScopeFactory scopeFactory,
    ILogger<SyncYooKassaPaymentsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!YooKassaConfig.IsEnabled)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var moneyService = scope.ServiceProvider.GetRequiredService<MoneyService>();
            var credited = await moneyService.SyncPendingYooKassaPaymentsAsync(context.CancellationToken);
            if (credited > 0)
            {
                logger.LogInformation("SyncYooKassaPaymentsJob credited/confirmed {Count} payment(s)", credited);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SyncYooKassaPaymentsJob failed");
        }
    }
}
