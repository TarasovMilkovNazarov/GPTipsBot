using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GPTipsBot.Repositories;
using GPTipsBot.Dtos;
using GPTipsBot.Api;
using System.Threading;
using GPTipsBot.UpdateHandlers;

namespace GPTipsBot
{
    public partial class TelegramBotWorker : IHostedService, IDisposable
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly UserRepository userRepository;
        private readonly MessageContextRepository messageRepository;
        private readonly GptAPI gptAPI;
        private readonly TelegramBotAPI telegramBotApi;
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly TypingStatus typingStatus;
        private bool onMaintenance = false;

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, 
            UserRepository userRepository, MessageContextRepository messageRepository, 
            GptAPI gptAPI, TelegramBotAPI telegramBotApi, MessageHandlerFactory messageHandlerFactory,
            TypingStatus typingStatus)
        {
            _logger = logger;
            this.userRepository = userRepository;
            this.messageRepository = messageRepository;
            this.gptAPI = gptAPI;
            this.telegramBotApi = telegramBotApi;
            this.messageHandlerFactory = messageHandlerFactory;
            this.typingStatus = typingStatus;
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var mainHandler = messageHandlerFactory.Create<MainHandler>();
            var extendedUpd = new UpdateWithCustomMessageDecorator(update, cancellationToken);

            try
            {
                await mainHandler.HandleAsync(extendedUpd, cancellationToken);
            }
            catch (Exception ex)
            {
                telegramBotApi.LogErrorMessageFromApiResponse(ex);
                await botClient.SendTextMessageAsync(update.Message.Chat.Id, BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);
                await typingStatus.Stop(cancellationToken);
            }
        }
    }
}
