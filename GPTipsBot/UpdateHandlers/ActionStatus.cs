using GPTipsBot.Dtos;
using GPTipsBot.Resources;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class ActionStatus
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<TelegramBotWorker> _logger;
        private int _serviceMessageId;
        private Timer _timer;

        public ActionStatus(ITelegramBotClient botClient, ILogger<TelegramBotWorker> logger)
        {
            this.botClient = botClient;
            this._logger = logger;
        }

        public async Task<long> Start(UserChatKey userKey, ChatAction chatAction, CancellationToken cancellationToken)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(BotUI.StopRequestButton, "/stopRequest"));
            var serviceMessage = await botClient.SendTextMessageAsync(userKey.ChatId, BotResponse.PleaseWaitMsg, 
                cancellationToken: cancellationToken, replyMarkup: inlineKeyboard);
            _serviceMessageId = serviceMessage.MessageId;

            var tokenSource = new CancellationTokenSource();
            MainHandler.userState[userKey].messageIdToCancellation.Add(_serviceMessageId, tokenSource);

            _timer = new((object o) =>
            {
                try
                {
                    botClient.SendChatActionAsync(userKey.ChatId, chatAction, cancellationToken: tokenSource.Token);
                }
                catch (Exception ex) { _logger.LogInformation($"Error while SendChatActionAsync {ex.Message}"); }

            }, null, 0, 8 * 1000);

            return serviceMessage.MessageId;
        }

        
        public async Task Stop(UserChatKey userKey,CancellationToken cancellationToken)
        {
            await botClient.DeleteMessageAsync(userKey.ChatId, _serviceMessageId, cancellationToken: cancellationToken);
            MainHandler.userState[userKey].messageIdToCancellation.Remove(_serviceMessageId);

            _timer?.Dispose();
        }
    }
} 
