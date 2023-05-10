using Telegram.Bot.Types;
using Telegram.Bot;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GPTipsBot.Repositories;
using GPTipsBot.Dtos;
using GPTipsBot.Api;
using System.Threading;

namespace GPTipsBot
{
    public partial class TelegramBotWorker : IHostedService, IDisposable
    {
        private readonly ILogger<TelegramBotWorker> _logger;
        private readonly UserRepository userRepository;
        private readonly MessageContextRepository messageRepository;
        private readonly GptAPI gptAPI;
        private readonly TelegramBotAPI telegramBotApi;
        private bool onMaintenance = false;

        public TelegramBotWorker(ILogger<TelegramBotWorker> logger, UserRepository userRepository,
            MessageContextRepository messageRepository, GptAPI gptAPI, TelegramBotAPI telegramBotApi)
        {
            _logger = logger;
            this.userRepository = userRepository;
            this.messageRepository = messageRepository;
            this.gptAPI = gptAPI;
            this.telegramBotApi = telegramBotApi;
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var chatId = update.Message.Chat.Id;

            try
            {
                if (HandleRemove(update) == null || 
                    HandleOnMaintenance(botClient, update.Message?.Text, chatId, cancellationToken) == null || 
                    HandleRateLimiting(botClient, chatId, update.Message.From.Id, cancellationToken) == null ||
                    FilterMessage(update) == null ||
                    HandleGroupOrChannelMessage(update.Message) == null) return;

                var telegramGptMessage = new TelegramGptMessage(update.Message);
                messageRepository.AddUserMessage(telegramGptMessage);

                var processCommand = await HandleCommand(botClient, telegramGptMessage, cancellationToken);
                if (processCommand == null) return;

                var typingInfo = await BroadcastTypingStatus(botClient, chatId, cancellationToken);
                await AskChatGpt(botClient, telegramGptMessage, cancellationToken);
                await StopStatusBroadcasting(botClient, typingInfo.serviceMessage.MessageId, chatId, typingInfo.statusTimer, cancellationToken);
                await SendChatGptReplyToUser(botClient, telegramGptMessage, cancellationToken);
            }
            catch (Exception ex)
            {
                telegramBotApi.LogErrorMessageFromApiResponse(ex);
                await botClient.SendTextMessageAsync(chatId, BotResponse.SomethingWentWrong, cancellationToken: cancellationToken);
            }
        }
    }
}
