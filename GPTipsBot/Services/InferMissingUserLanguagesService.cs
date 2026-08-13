using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services;

/// <summary>
/// Backfills BotSettings.Language from user messages. Yields immediately so host/polling start is not blocked.
/// </summary>
public sealed class InferMissingUserLanguagesService(
    IServiceScopeFactory scopeFactory,
    ILogger<InferMissingUserLanguagesService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<InferMissingUserLanguages>();
                var updated = await job.RunAsync(stoppingToken);
                logger.LogInformation("Missing-language backfill finished, users updated: {Count}", updated);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Missing-language backfill failed; bot keeps running");
            }

            try
            {
                await Task.Delay(RepeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
