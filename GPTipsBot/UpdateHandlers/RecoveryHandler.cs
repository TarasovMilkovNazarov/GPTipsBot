using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class RecoveryHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly MessageRepository messageRepository;
        private static readonly Dictionary<long, Queue<UpdateDecorator>> ChatToInformAboutRecovery = new();

        public RecoveryHandler(ITelegramBotClient botClient,
            MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository)
        {
            this.botClient = botClient;
            this.messageRepository = messageRepository;
            SetNextHandler(messageHandlerFactory.Create<OnAdminCommandHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (update.UserChatKey == null)
            {
                await base.HandleAsync(update);
                return;
            }

            var chatId = update.UserChatKey.ChatId;

            // todo проверка игнорит недавние сообщения, не понятно
            if (IsEarlyRecovery(update)) return;

            if (IsLateRecovery(update))
            {
                if (update.IsGroupOrChannel)
                {
                    return;
                }

                messageRepository.AddMessage(update.Message);

                if (ChatToInformAboutRecovery.TryGetValue(chatId, out var value))
                    value.Enqueue(update);
                else
                {
                    var q = new Queue<UpdateDecorator>();
                    q.Enqueue(update);
                    ChatToInformAboutRecovery.Add(chatId, q);

                    await botClient.SendTextMessageAsync(chatId, BotResponse.Recovered);
                }

                return;
            }

            ChatToInformAboutRecovery.Remove(chatId);

            // Call next handler
            await base.HandleAsync(update);
        }

        private static bool IsLateRecovery(UpdateDecorator update)
        {
            return UpdateHandlerEntryPoint.Start - update.Message.CreatedAt >= TimeSpan.FromMinutes(2);
        }

        private static bool IsEarlyRecovery(UpdateDecorator update)
        {
            return UpdateHandlerEntryPoint.Start - update.Message.CreatedAt >= TimeSpan.FromSeconds(35);
        }
    }
}
