using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class CrudHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageRepository messageRepository;
        private readonly ITelegramBotClient botClient;

        public CrudHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository, ITelegramBotClient botClient)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<ChatGptHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (MainHandler.userState.ContainsKey(update.TelegramGptMessage.UserKey) && 
                MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState == Enums.UserStateEnum.AwaitingImage)
            {
                update.TelegramGptMessage.ContextBound = false;
                SetNextHandler(messageHandlerFactory.Create<ImageGeneratorHandler>());
            }
            if (MainHandler.userState.ContainsKey(update.TelegramGptMessage.UserKey) && 
                MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState == Enums.UserStateEnum.SendingFeedback)
            {
                update.TelegramGptMessage.ContextBound = false;
                MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.None;
                update.TelegramGptMessage.Text = "Отзыв: " + update.TelegramGptMessage.Text;
                await botClient.SendTextMessageWithMenuKeyboard(update.TelegramGptMessage.UserKey.ChatId, Api.BotResponse.Thanks, cancellationToken);
                SetNextHandler(null);
            }

            messageRepository.AddUserMessage(update.TelegramGptMessage);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
