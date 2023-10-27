using GPTipsBot;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Abstract;

namespace Telegram.Bot.Services
{
    // Compose Receiver and UpdateHandler implementation
    public class ReceiverService : ReceiverServiceBase<UpdateHandlerEntryPoint>
    {
        public ReceiverService(
            ITelegramBotClient botClient,
            IServiceProvider serviceProvider,
            ILogger<ReceiverServiceBase<UpdateHandlerEntryPoint>> log)
            : base(botClient, serviceProvider, log)
        {
        }
    }
}