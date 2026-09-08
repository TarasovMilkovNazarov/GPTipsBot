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
        if (crossed.Count == 0 && recovered.Count == 0)
            return;

        state.Apply(account.Id, crossed, recovered);

        if (crossed.Count == 0)
            return;

        var text = YandexBillingBalanceAlerts.FormatMessage(account.Name, account.Balance, crossed);
        foreach (var adminId in AppConfig.AdminIds)
        {
            try
            {
                await botClient.SendMessage(adminId, text, cancellationToken: token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send Yandex Cloud balance alert to admin {AdminId}", adminId);
            }
        }
    }
}
