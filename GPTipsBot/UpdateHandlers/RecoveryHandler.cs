using GPTipsBot.Api;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class RecoveryHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly MessageContextRepository messageRepository;
        private static readonly Dictionary<long, Queue<Update>> userToUpdatesQueue = new Dictionary<long, Queue<Update>>();

        public RecoveryHandler(ITelegramBotClient botClient,
            MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository)
        {
            this.botClient = botClient;
            this.messageRepository = messageRepository;
            SetNextHandler(messageHandlerFactory.Create<OnMaintenanceHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (update.Update.Message?.From == null || update.TelegramGptMessage == null)
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var telegramId = update.Update.Message.From.Id;

            if (TelegramBotWorker.Start - update.Update.Message?.Date >= TimeSpan.FromMinutes(2))
            {
                messageRepository.AddUserMessage(update.TelegramGptMessage);
                

                if (userToUpdatesQueue.ContainsKey(telegramId))
                    userToUpdatesQueue[telegramId].Enqueue(update.Update);
                else
                {
                    var q = new Queue<Update>();
                    q.Enqueue(update.Update);
                    userToUpdatesQueue.Add(telegramId, q);
                    
                    await botClient.SendTextMessageAsync(update.Update.Message.Chat.Id, 
                        BotResponse.Recovered, cancellationToken: update.CancellationToken);
                };

                return;
            }
            else if(userToUpdatesQueue.ContainsKey(telegramId))
                userToUpdatesQueue.Remove(telegramId);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
