using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Repositories;
using GPTipsBot.Services;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class DeleteUserHandler : BaseMessageHandler
    {
        private readonly UserRepository userRepository;

        public DeleteUserHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository)
        {
            this.userRepository = userRepository;
            SetNextHandler(messageHandlerFactory.Create<OnMaintenanceHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            if (update.Update.MyChatMember?.NewChatMember.Status == ChatMemberStatus.Kicked)
            {
                userRepository.SoftlyRemoveUser(update.Update.MyChatMember.From.Id);

                return;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
