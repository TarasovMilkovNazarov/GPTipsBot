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
            OnAdminCommandHandler onAdminCommandHandler, MessageRepository messageRepository)
        {
            this.botClient = botClient;
            this.messageRepository = messageRepository;
            SetNextHandler(onAdminCommandHandler);
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;

            if (IsLateRecovery(update))
            {
                if (update.IsGroupOrChannel)
                {
                    return;
                }

                await messageRepository.AddAsync(update.Message);

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
            return update.Message.CreatedAt <= UpdateHandlerEntryPoint.Start - TimeSpan.FromMinutes(2);
        }
    }
}
