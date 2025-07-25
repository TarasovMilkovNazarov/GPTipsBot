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
    public class RecoveryNotificationHandler(
        ITelegramBotClient botClient,
        MessageRepository messageRepository)
        : BaseMessageHandler
    {
        private static readonly ConcurrentDictionary<long, bool> ChatToInformAboutRecovery = new();

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;

            if (update.IsExpired())
            {
                if (update.IsGroupOrChannel)
                {
                    return;
                }

                await messageRepository.AddAsync(update.Message);

                if (ChatToInformAboutRecovery.TryAdd(chatId, true))
                {
                    await botClient.SendMessage(chatId, BotResponse.Recovered);
                }

                return;
            }

            ChatToInformAboutRecovery.Remove(chatId, out _);
        }
    }
}
