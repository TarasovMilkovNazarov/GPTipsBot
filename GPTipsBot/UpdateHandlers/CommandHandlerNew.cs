using GPTipsBot.Dtos.Commands;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class CommandHandlerNew : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<CommandHandlerNew> logger;
        private readonly CommandService commandService;

        public CommandHandlerNew(HandlerFactory messageHandlerFactory, ITelegramBotClient botClient, 
            ILogger<CommandHandlerNew> logger, CommandService commandService)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.commandService = commandService;
            SetNextHandler(messageHandlerFactory.Create<CrudHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var messageText = update.Message.Text;
            var chatId = update.UserChatKey.ChatId;

            if (!commandService.TryGetCommand(messageText, out var command))
            {
                await base.HandleAsync(update);
                return;
            }

            update.Message.ContextBound = false;

            await command!.ApplyAsync(update);
            if (command is IRepliable repliable) { await repliable.ReplyAsync(botClient, chatId); }
        }
    }
}
