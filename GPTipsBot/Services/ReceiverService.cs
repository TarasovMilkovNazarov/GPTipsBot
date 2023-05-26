using GPTipsBot;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Abstract;

namespace Telegram.Bot.Services
{
    // Compose Receiver and UpdateHandler implementation
    public class ReceiverService : ReceiverServiceBase<TelegramBotWorker>
    {
        public ReceiverService(
            ITelegramBotClient botClient,
            TelegramBotWorker updateHandler,
            ILogger<ReceiverServiceBase<TelegramBotWorker>> logger)
            : base(botClient, updateHandler, logger)
        {
        }
    }
}