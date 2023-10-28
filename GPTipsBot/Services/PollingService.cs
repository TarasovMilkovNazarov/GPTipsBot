using Microsoft.Extensions.Logging;
using Telegram.Bot.Abstract;
using Telegram.Bot.Services;

namespace GPTipsBot.Services
{
    // Compose Polling and ReceiverService implementations
    public class PollingService : PollingServiceBase<ReceiverService>
    {
        public PollingService(IServiceProvider serviceProvider, ILogger<PollingService> log)
            : base(serviceProvider, log)
        {
        }
    }
}
