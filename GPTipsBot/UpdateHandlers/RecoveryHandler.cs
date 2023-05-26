using GPTipsBot.Api;
using GPTipsBot.Extensions;
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
        private static readonly Dictionary<long, Queue<Update>> chatToInformAboutRecovery = new Dictionary<long, Queue<Update>>();

        public RecoveryHandler(ITelegramBotClient botClient,
            MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository)
        {
            this.botClient = botClient;
            this.messageRepository = messageRepository;
            SetNextHandler(messageHandlerFactory.Create<OnAdminCommandHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (update.Update.Message?.From == null || update.TelegramGptMessage == null)
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var chatId = update.Update.Message.Chat.Id;

            if (TelegramBotWorker.Start - update.Update.Message?.Date >= TimeSpan.FromMinutes(2))
            {
                messageRepository.AddUserMessage(update.TelegramGptMessage);
                

                if (chatToInformAboutRecovery.ContainsKey(chatId))
                    chatToInformAboutRecovery[chatId].Enqueue(update.Update);
                else
                {
                    var q = new Queue<Update>();
                    q.Enqueue(update.Update);
                    chatToInformAboutRecovery.Add(chatId, q);
                    
                    await botClient.SendTextMessageAsync(update.Update.Message.Chat.Id, 
                        BotResponse.Recovered, cancellationToken: update.CancellationToken);
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
