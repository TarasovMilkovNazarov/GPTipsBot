using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Abstract;

namespace Telegram.Bot.Services
{
    // Compose Receiver and UpdateHandler implementation
    public class ReceiverService(
        ITelegramBotClient botClient,
        IServiceProvider serviceProvider,
        ILogger<ReceiverServiceBase<MainHandler>> log)
        : ReceiverServiceBase<MainHandler>(botClient, serviceProvider, log);
}