using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class CommandHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly UserRepository userRepository;
        private readonly ITelegramBotClient botClient;
        private readonly TelegramBotAPI telegramBotAPI;

        public CommandHandler(MessageHandlerFactory messageHandlerFactory,UserRepository userRepository, ITelegramBotClient botClient, 
            TelegramBotAPI telegramBotAPI)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.userRepository = userRepository;
            this.botClient = botClient;
            this.telegramBotAPI = telegramBotAPI;

            var crudHandler = messageHandlerFactory.Create<CrudHandler>();
            crudHandler.SetNextHandler(null);
            SetNextHandler(messageHandlerFactory.Create<CrudHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var messageText = update.TelegramGptMessage.Message;
            var chatId = update.TelegramGptMessage.ChatId;
            var isCommand = false;
            
            if (messageText.StartsWith("/start"))
            {
                update.TelegramGptMessage.Source = TelegramService.GetSource(messageText);
                //userRepository.CreateUpdateUser(update.TelegramGptMessage);
                await botClient.SendTextMessageAsync(chatId, BotResponse.Greeting, cancellationToken:cancellationToken);

                isCommand = true;
            }
            else if (messageText == "/help")
            {
                var desc = telegramBotAPI.GetMyDescription();
                await botClient.SendTextMessageAsync(chatId, desc, cancellationToken:cancellationToken);

                isCommand = true;
            }

            if (isCommand)
            {
                return;

                var crudHandler = messageHandlerFactory.Create<CrudHandler>();
                crudHandler.SetNextHandler(null);

                SetNextHandler(crudHandler);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
