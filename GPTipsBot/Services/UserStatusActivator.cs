using GPTipsBot.Dtos;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services
{
    /// <summary>
    /// This service trigger status in header of bot
    /// when doing long running actions(like "...Sending photo" or "...Typing")
    /// </summary>
    public class UserStatusActivator
    {
        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<UserStatusActivator> _logger;
        private int _serviceMessageId;

        /// <summary>
        ///  For status persistence action till the end of processing or choosing inline /stop_requests buttons
        /// </summary>
        private Timer? _timer;

        public UserStatusActivator(ITelegramBotClient botClient, ILogger<UserStatusActivator> logger)
        {
            _botClient = botClient;
            _logger = logger;
        }

        public async Task<long> Start(UserChatKey userKey, ChatAction chatAction)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(BotUI.StopRequestButton, "/stopRequest"));
            var serviceMessage = await _botClient.SendTextMessageAsync
                (userKey.ChatId, BotResponse.PleaseWaitMsg, replyMarkup: inlineKeyboard);
            _serviceMessageId = serviceMessage.MessageId;

            var tokenSource = new CancellationTokenSource();
            MainHandler.UserState[userKey].MessageIdToCancellation.Add(serviceMessage.MessageId, tokenSource);

            _timer = new Timer(_ =>
            {
                try
                {
                    if (tokenSource.Token.IsCancellationRequested)
                    {
                        _logger.LogInformation("Request ChatAction to telegram was canceled");
                        return;
                    }

                    _botClient.SendChatActionAsync(userKey.ChatId, chatAction, cancellationToken: tokenSource.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error while SendChatActionAsync {ex.Message}");
                }

            }, null, 0, 8 * 1000);

            return serviceMessage.MessageId;
        }

        
        public async Task Stop(UserChatKey userKey)
        {
            if (_serviceMessageId != 0)
            {
                await _botClient.DeleteMessageAsync(userKey.ChatId, _serviceMessageId);
            }

            MainHandler.UserState[userKey].MessageIdToCancellation.Remove(_serviceMessageId);

            if (_timer != null)
            {
                await _timer.DisposeAsync();
            }
        }
    }
} 
