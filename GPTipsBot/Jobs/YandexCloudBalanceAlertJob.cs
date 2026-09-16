using GPTipsBot.Config;
using GPTipsBot.Services.YandexCloud;
using Microsoft.Extensions.Logging;
using Quartz;
using Telegram.Bot;

namespace GPTipsBot.Jobs;

[DisallowConcurrentExecution]
public class YandexCloudBalanceAlertJob(
    YandexBillingAccountClient billingClient,
    YandexBillingAlertState state,
    ITelegramBotClient botClient,
    ILogger<YandexCloudBalanceAlertJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var accounts = await billingClient.ListAccountsAsync(context.CancellationToken);
            foreach (var account in accounts.Where(a => a.Active))
            {
                await CheckAccountAsync(account, context.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "YandexCloudBalanceAlertJob failed");
        }
    }

    private async Task CheckAccountAsync(YandexBillingAccount account, CancellationToken token)
    {
        var notified = state.Snapshot(account.Id);
        var crossed = YandexBillingBalanceAlerts.NewlyCrossed(account.Balance, notified);
        var recovered = YandexBillingBalanceAlerts.Recovered(account.Balance, notified);
        if (crossed.Count > 0 || recovered.Count > 0)
        {
            state.Apply(account.Id, crossed, recovered);

            if (crossed.Count > 0)
            {
                var text = YandexBillingBalanceAlerts.FormatMessage(account.Name, account.Balance, crossed);
                await NotifyAdminsAsync(text, token);
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (spentToday, shouldAlert) = state.TrackDailyConsumption(account.Id, account.Balance, today);
        if (shouldAlert)
        {
            var text = YandexBillingBalanceAlerts.FormatDailyConsumptionMessage(account.Name, spentToday);
            await NotifyAdminsAsync(text, token);
        }
    }

    private async Task NotifyAdminsAsync(string text, CancellationToken token)
    {
        foreach (var adminId in AppConfig.AdminIds)
        {
            try
            {
                await botClient.SendMessage(adminId, text, cancellationToken: token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send Yandex Cloud alert to admin {AdminId}", adminId);
            }
        }
    }
}
