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
    public class MainHandler : BaseMessageHandler
    {
        private readonly UserRepository userRepository;

        public MainHandler(MessageHandlerFactory messageHandlerFactory, UserRepository userRepository)
        {
            this.userRepository = userRepository;
            SetNextHandler(messageHandlerFactory.Create<DeleteUserHandler>());
        }
    }
}
