using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class CrudHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageRepository messageRepository;
        private readonly ITelegramBotClient botClient;

        public CrudHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository, ITelegramBotClient botClient, UserRepository userRepository)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<ChatGptHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var value = MainHandler.userState.GetValueOrDefault(update.UserChatKey);
            switch (value?.CurrentState)
            {
                case UserStateEnum.AwaitingImage:
                    update.Message.ContextBound = false;
                    SetNextHandler(messageHandlerFactory.Create<ImageGeneratorHandler>());
                    break;
                case UserStateEnum.AwaitingTextRecognitionImage:
                    update.Message.ContextBound = false;
                    SetNextHandler(messageHandlerFactory.Create<ImageTextRecognitionHandler>());
                    break;
                case UserStateEnum.SendingFeedback:
                    update.Message.ContextBound = false;
                    value.CurrentState = UserStateEnum.None;
                    update.Message.Text = $"Отзыв: {update.Message.Text}";
                    await botClient.SendTextMessageWithMenuKeyboard(update.UserChatKey.ChatId, BotResponse.Thanks);
                    return;
            }

            messageRepository.AddMessage(update.Message);

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
