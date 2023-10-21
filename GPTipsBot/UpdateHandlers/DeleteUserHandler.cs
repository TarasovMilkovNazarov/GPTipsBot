using GPTipsBot.Repositories;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class DeleteUserHandler : BaseMessageHandler
    {
        private readonly UserRepository userRepository;

        public DeleteUserHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository)
        {
            this.userRepository = userRepository;
            SetNextHandler(messageHandlerFactory.Create<RecoveryHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (update.ChatMemberStatus == ChatMemberStatus.Kicked)
            {
                userRepository.SoftlyRemoveUser(update.UserChatKey.Id);

                return;
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
