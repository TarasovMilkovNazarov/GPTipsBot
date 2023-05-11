using GPTipsBot.Api;
using GPTipsBot.Repositories;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class UserToGptHandler : BaseMessageHandler
    {
        private readonly MessageContextRepository messageRepository;
        private readonly GptAPI gptAPI;
        private readonly TypingStatus typingStatus;

        public UserToGptHandler(MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository, GptAPI gptAPI, TypingStatus typingStatus)
        {
            this.messageRepository = messageRepository;
            this.gptAPI = gptAPI;
            this.typingStatus = typingStatus;
            SetNextHandler(messageHandlerFactory.Create<GptToUserHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.TelegramGptMessage;

            await typingStatus.Start(update.Update.Message.Chat.Id, cancellationToken);
            var gtpResponse = await gptAPI.SendMessage(message);
            message.Reply = gtpResponse.text;
            messageRepository.AddBotResponse(message);
            await typingStatus.Stop(cancellationToken);

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
