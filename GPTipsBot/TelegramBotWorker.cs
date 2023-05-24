using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GPTipsBot.Repositories;
using GPTipsBot.Dtos;
using GPTipsBot.Api;
using System.Threading;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Exceptions;
using GPTipsBot.Extensions;

namespace GPTipsBot
{
    public partial class TelegramBotWorker : IHostedService, IDisposable
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly GptAPI gptAPI;
        private readonly TelegramBotAPI telegramBotApi;
        private readonly MessageHandlerFactory messageHandlerFactory;

        public static readonly Dictionary<long, Queue<Update>> userToUpdatesQueue = new Dictionary<long, Queue<Update>>();
        public static DateTime Start { get; private set; }

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, GptAPI gptAPI, 
            TelegramBotAPI telegramBotApi, MessageHandlerFactory messageHandlerFactory)
        {
            _logger = logger;
            this.gptAPI = gptAPI;
            this.telegramBotApi = telegramBotApi;
            this.messageHandlerFactory = messageHandlerFactory;
            Start = DateTime.UtcNow;
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
                if (ex is ApiRequestException apiEx || update.Message == null)
                {
                    return;
                }

                await botClient.SendTextMessageWithMenuKeyboard(update.Message.Chat.Id, BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);
            }
        }
    }
}
