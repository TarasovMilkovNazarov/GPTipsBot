using GPTipsBot.Api;
using GPTipsBot.Enums;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class CrudHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageContextRepository messageRepository;
        private readonly ITelegramBotClient botClient;

        public CrudHandler(MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository, ITelegramBotClient botClient)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<UserToGptHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (MainHandler.userState.ContainsKey(update.TelegramGptMessage.UserKey) && 
                MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState == Enums.UserStateEnum.AwaitingImage)
            {
                update.TelegramGptMessage.ContextBound = false;
                SetNextHandler(messageHandlerFactory.Create<ImageGeneratorToUserHandler>());
            }
            if (MainHandler.userState.ContainsKey(update.TelegramGptMessage.UserKey) && 
                MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState == Enums.UserStateEnum.SendingFeedback)
            {
                update.TelegramGptMessage.ContextBound = false;
                MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState = UserStateEnum.None;
                update.TelegramGptMessage.Text = "Отзыв: " + update.TelegramGptMessage.Text;
                await botClient.SendTextMessageAsync(update.TelegramGptMessage.UserKey.ChatId, BotResponse.Thanks, replyMarkup: new ReplyKeyboardRemove());
                SetNextHandler(null);
            }

            messageRepository.AddUserMessage(update.TelegramGptMessage);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
