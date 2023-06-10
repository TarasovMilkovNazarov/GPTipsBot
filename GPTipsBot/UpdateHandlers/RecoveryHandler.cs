using GPTipsBot.Repositories;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class RecoveryHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly MessageRepository messageRepository;
        private static readonly Dictionary<long, Queue<UpdateDecorator>> chatToInformAboutRecovery = new Dictionary<long, Queue<UpdateDecorator>>();

        public RecoveryHandler(ITelegramBotClient botClient,
            MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository)
        {
            this.botClient = botClient;
            this.messageRepository = messageRepository;
            SetNextHandler(messageHandlerFactory.Create<OnAdminCommandHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            if (update.UserChatKey == null)
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var chatId = update.UserChatKey.ChatId;

            if (TelegramBotWorker.Start - update.Message.CreatedAt >= TimeSpan.FromMinutes(2))
            {
                messageRepository.AddMessage(update.Message);
                

                if (chatToInformAboutRecovery.ContainsKey(chatId))
                    chatToInformAboutRecovery[chatId].Enqueue(update);
                else
                {
                    var q = new Queue<UpdateDecorator>();
                    q.Enqueue(update);
                    chatToInformAboutRecovery.Add(chatId, q);
                    
                    await botClient.SendTextMessageAsync(chatId,
                        Api.BotResponse.Recovered, cancellationToken: update.CancellationToken);
                };

                return;
            }
            else if(chatToInformAboutRecovery.ContainsKey(chatId))
                chatToInformAboutRecovery.Remove(chatId);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
