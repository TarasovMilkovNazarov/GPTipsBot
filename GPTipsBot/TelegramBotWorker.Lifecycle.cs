using Microsoft.Extensions.Logging;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace GPTipsBot
{
    public partial class TelegramBotWorker
    {
        public async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            telegramBotApi.LogErrorMessageFromApiResponse(exception);
            // Cooldown in case of network connection error
            if (exception is RequestException)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var botClient = new TelegramBotClient(AppConfig.TelegramToken);

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions
            );

            await botClient.SetMyCommandsAsync(new List<BotCommand>() { 
                new BotCommand { Command = "/start", Description = "Начать пользоваться ботом" },
                new BotCommand { Command = "/help", Description = "Инструкция по применению" },
                new BotCommand { Command = "/image", Description = "Создать изображение по текстовому описанию" },
            });

            var me = await botClient.GetMeAsync();
            _logger.LogInformation($"Start listening for @{me.Username}");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Telegram background service is stopping.");

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation("Telegram bs disposing");
        }
    }
}
