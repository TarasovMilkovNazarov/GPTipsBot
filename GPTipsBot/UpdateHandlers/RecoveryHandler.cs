using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class RecoveryHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly MessageRepository messageRepository;
        private static readonly Dictionary<long, Queue<UpdateDecorator>> chatToInformAboutRecovery = new ();

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

            if (update.Message.CreatedAt < UpdateHandlerEntryPoint.Start)
            {
                return;
            }

            if (UpdateHandlerEntryPoint.Start - update.Message.CreatedAt >= TimeSpan.FromMinutes(2))
            {
                if (update.IsGroupOrChannel)
                {
                    return;
                }

                messageRepository.AddMessage(update.Message);

                if (chatToInformAboutRecovery.ContainsKey(chatId))
                    chatToInformAboutRecovery[chatId].Enqueue(update);
                else
                {
                    var q = new Queue<UpdateDecorator>();
                    q.Enqueue(update);
                    chatToInformAboutRecovery.Add(chatId, q);
                    
                    await botClient.SendTextMessageAsync(chatId, BotResponse.Recovered);
                }

                return;
            }
            else if(chatToInformAboutRecovery.ContainsKey(chatId))
                chatToInformAboutRecovery.Remove(chatId);

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
