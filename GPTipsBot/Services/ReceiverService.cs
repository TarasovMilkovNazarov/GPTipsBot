using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Abstract;

namespace Telegram.Bot.Services
{
    // Compose Receiver and UpdateHandler implementation
    public class ReceiverService : ReceiverServiceBase<MainHandler>
    {
        public ReceiverService(
            ITelegramBotClient botClient,
            IServiceProvider serviceProvider,
            ILogger<ReceiverServiceBase<MainHandler>> log)
            : base(botClient, serviceProvider, log)
        {
        }
    }
}