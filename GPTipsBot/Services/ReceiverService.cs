using GPTipsBot;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Abstract;

namespace Telegram.Bot.Services
{
    // Compose Receiver and UpdateHandler implementation
    public class ReceiverService : ReceiverServiceBase<FirstUpdateHandler>
    {
        public ReceiverService(
            ITelegramBotClient botClient,
            IServiceProvider serviceProvider,
            ILogger<ReceiverServiceBase<FirstUpdateHandler>> log)
            : base(botClient, serviceProvider, log)
        {
        }
    }
}