using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using Telegram.Bot;
using GPTipsBot.Exceptions;
using GPTipsBot.Services;
using GPTipsBot.Resources;
using OpenAI.ObjectModels.ResponseModels;

namespace GPTipsBot.UpdateHandlers
{
    public class ChatGptHandler : BaseMessageHandler
    {
        private readonly MessageRepository _messageRepository;
        private readonly IGpt _gptService;
        private readonly UserStatusActivator _typingStatus;
        private readonly ILogger<ChatGptHandler> _log;
        private readonly ITelegramBotClient _botClient;
        private readonly IAdvertisementClient _advertisementClient;
        private readonly TelejetAdClient _telejetAdClient;
        private readonly UserService _userService;
        private readonly ApplicationContext _context;

        public ChatGptHandler(
            MessageRepository messageRepository,
            IGpt gptService,
            UserStatusActivator typingStatus,
            ILogger<ChatGptHandler> log,
            ITelegramBotClient botClient,
            IAdvertisementClient gramadsAdvertisementClient,
            TelejetAdClient telejetAdClient,
            UserService userService,
            ApplicationContext context)
        {
            _messageRepository = messageRepository;
            _gptService = gptService;
            _typingStatus = typingStatus;
            _log = log;
            _botClient = botClient;
            _advertisementClient = gramadsAdvertisementClient;
            _telejetAdClient = telejetAdClient;
            _userService = userService;
            _context = context;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var shortMessage = update.Message.Text.Truncate(30) + "...";
            MessageDto? gptResponse = null;

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var request = await _messageRepository.AddAsync(update.Message);
            var successPayment = await _userService.PayForGpt(update.UserChatKey.Id);

            if (!successPayment)
            {
                await dbTransaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.NoFreeRequests,
                    replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
                return;
            }

            try
            {
                var serviceMessageId = await _typingStatus.Start(update.UserChatKey, Telegram.Bot.Types.Enums.ChatAction.Typing);

                var sw = Stopwatch.StartNew();
                var token = Dispatcher.UserState[update.UserChatKey].MessageIdToCancellation[serviceMessageId].Token;

                ChatCompletionCreateResponse? response = null;

                try
                {
                    response = await _gptService.SendMessage(update, token);
                }
                catch (OperationCanceledException)
                {
                    _log.LogInformation("Request to openai service with promt '{promt}' was canceled", shortMessage);
                    return;
                }
                catch (ChatGptException ex)
                {
                    _log.LogError("Failed request to OpenAi service: [{Code}] {Message}", response?.Error?.Code, response?.Error?.Message);
                    await _botClient.SendTextMessageAsync(
                        update.UserChatKey.ChatId,
                        BotResponse.SomethingWentWrong,
                        (int)update.Message.TelegramMessageId!, cancellationToken: token
                        );

                    return;
                }
                finally
                {
                    sw.Stop();
                }

                _log.LogInformation("Get response to prompt '{prompt}' takes {duration}s", shortMessage, sw.Elapsed.TotalSeconds);

                gptResponse = new MessageDto(update.UserChatKey)
                {
                    Text = response.Choices.FirstOrDefault()?.Message.Content ?? "",
                    Role = Enums.MessageOwner.Assistant,
                    ContextBound = true,
                };

                Guard.Against.Null(gptResponse);

                await _messageRepository.AddAsync(gptResponse, request);
                await _botClient.TrySendMarkdown2MessageAsync(update.UserChatKey.ChatId, gptResponse.Text, (int)update.Message.TelegramMessageId!);
            }
            catch (ClientException ex)
            {
                _log.LogInformation(ex, shortMessage);
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId, ex.Message,
                    replyToMessageId: (int)update.Message.TelegramMessageId!);
                return;
            }
            finally
            {
                await _typingStatus.Stop(update.UserChatKey);
            }

            await dbTransaction.CommitAsync();
            await _advertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            await _telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
        }
    }
}