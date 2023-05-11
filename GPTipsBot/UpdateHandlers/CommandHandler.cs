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
        private readonly UserRepository userRepository;
        private readonly ITelegramBotClient botClient;
        private readonly TelegramBotAPI telegramBotAPI;

        public CommandHandler(MessageHandlerFactory messageHandlerFactory,UserRepository userRepository, ITelegramBotClient botClient, 
            TelegramBotAPI telegramBotAPI)
        {
            this.userRepository = userRepository;
            this.botClient = botClient;
            this.telegramBotAPI = telegramBotAPI;

            SetNextHandler(messageHandlerFactory.Create<CrudHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var messageText = update.TelegramGptMessage.Message;
            var chatId = update.TelegramGptMessage.ChatId;
            
            if (messageText.StartsWith("/start"))
            {
                update.TelegramGptMessage.Source = TelegramService.GetSource(messageText);
                userRepository.CreateUpdateUser(update.TelegramGptMessage);
                await botClient.SendTextMessageAsync(chatId, BotResponse.Greeting, cancellationToken:cancellationToken);

                return;
            }
            else if (messageText == "/help")
            {
                var desc = telegramBotAPI.GetMyDescription();
                await botClient.SendTextMessageAsync(chatId, desc, cancellationToken:cancellationToken);

                return;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
