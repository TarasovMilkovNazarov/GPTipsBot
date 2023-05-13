using GPTipsBot.Api;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class TypingStatus
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<TelegramBotWorker> _logger;
        private long? _chatId;
        private int _serviceMessageId;
        private Timer _timer;

        public TypingStatus(ITelegramBotClient botClient, ILogger<TelegramBotWorker> logger)
        {
            this.botClient = botClient;
            this._logger = logger;
        }

        public async Task Start(long chatId, CancellationToken cancellationToken)
        {
            this._chatId = chatId;

            var serviceMessage = await botClient.SendTextMessageAsync(chatId, BotResponse.Typing, cancellationToken: cancellationToken);
            _serviceMessageId = serviceMessage.MessageId;

            _timer = new((object o) =>
            {
                try
                {
                    botClient.SendChatActionAsync(chatId, ChatAction.Typing, cancellationToken);
                }
                catch (Exception ex) { _logger.LogInformation($"Error while SendChatActionAsync {ex.Message}"); }

            }, null, 0, 8 * 1000);
        }

        
        public async Task Stop(CancellationToken cancellationToken)
        {
            if (!_chatId.HasValue)
            {
                return;
            }

            await botClient.DeleteMessageAsync(_chatId, _serviceMessageId, cancellationToken: cancellationToken);
            _timer?.Dispose();
        }
    }
}
