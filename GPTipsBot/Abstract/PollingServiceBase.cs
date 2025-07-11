using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Telegram.Bot.Abstract;

public abstract class PollingServiceBase<TReceiverService> : BackgroundService
    where TReceiverService : IReceiverService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _log;

    internal PollingServiceBase(
        IServiceProvider serviceProvider,
        ILogger log)
    {
        _serviceProvider = serviceProvider;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Starting polling service");
        await DoWork(stoppingToken);
    }

    private async Task DoWork(CancellationToken stoppingToken)
    {
        var receiver = _serviceProvider.GetRequiredService<TReceiverService>();
        await receiver.ReceiveAsync(stoppingToken);
    }
}