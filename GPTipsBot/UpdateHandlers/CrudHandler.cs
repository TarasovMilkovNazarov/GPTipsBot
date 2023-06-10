using GPTipsBot.Api;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
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
        private readonly MessageRepository messageRepository;
        private readonly ITelegramBotClient botClient;

        public CrudHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository, ITelegramBotClient botClient)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<ChatGptHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            if (MainHandler.userState.ContainsKey(update.UserChatKey) && 
                MainHandler.userState[update.UserChatKey].CurrentState == Enums.UserStateEnum.AwaitingImage)
            {
                update.Message.ContextBound = false;
                SetNextHandler(messageHandlerFactory.Create<ImageGeneratorHandler>());
            }
            if (MainHandler.userState.ContainsKey(update.UserChatKey) && 
                MainHandler.userState[update.UserChatKey].CurrentState == Enums.UserStateEnum.SendingFeedback)
            {
                update.Message.ContextBound = false;
                MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                update.Message.Text = "Отзыв: " + update.Message.Text;
                await botClient.SendTextMessageWithMenuKeyboard(update.UserChatKey.ChatId, Api.BotResponse.Thanks, cancellationToken);
                SetNextHandler(null);
            }

            messageRepository.AddMessage(update.Message);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
