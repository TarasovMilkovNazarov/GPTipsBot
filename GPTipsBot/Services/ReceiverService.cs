using GPTipsBot;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Abstract;

namespace Telegram.Bot.Services
{
    // Compose Receiver and UpdateHandler implementation
    public class ReceiverService : ReceiverServiceBase<UpdateFirewall>
    {
        public ReceiverService(
            ITelegramBotClient botClient,
            IServiceProvider serviceProvider,
            ILogger<ReceiverServiceBase<UpdateFirewall>> log)
            : base(botClient, serviceProvider, log)
        {
        }
    }
}