using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Telegram.Bot.Abstract;

public abstract class PollingServiceBase<TReceiverService> : BackgroundService
    where TReceiverService : IReceiverService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger log;

    internal PollingServiceBase(
        IServiceProvider serviceProvider,
        ILogger log)
    {
        this.serviceProvider = serviceProvider;
        this.log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Starting polling service");
        await DoWork(stoppingToken);
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        var receiver = serviceProvider.GetRequiredService<TReceiverService>();
        await receiver.ReceiveAsync(stoppingToken);
    }
}