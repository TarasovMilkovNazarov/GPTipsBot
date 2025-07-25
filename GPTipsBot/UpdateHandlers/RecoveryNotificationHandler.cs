using System.Collections.Concurrent;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    /// <summary>
    /// Send message after period of unavailability
    /// When bot starts then it receives unhandled user's messages which could be in order.
    /// Try to answer only one of them
    /// </summary>
    public class RecoveryNotificationHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly MessageRepository _messageRepository;
        private static readonly ConcurrentDictionary<long, bool> ChatToInformAboutRecovery = new();

        public RecoveryNotificationHandler(ITelegramBotClient botClient,
            MessageRepository messageRepository)
        {
            _botClient = botClient;
            _messageRepository = messageRepository;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;

            if (update.IsExpired())
            {
                if (update.IsGroupOrChannel)
                {
                    return;
                }

                await _messageRepository.AddAsync(update.Message);

                if (ChatToInformAboutRecovery.TryAdd(chatId, true))
                {
                    await _botClient.SendMessage(chatId, BotResponse.Recovered);
                }

                return;
            }

            ChatToInformAboutRecovery.Remove(chatId, out _);
        }
    }
}
