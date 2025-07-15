using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using GPTipsBot.Dtos;
using Telegram.Bot;
using GPTipsBot.Exceptions;
using GPTipsBot.Services;
using GPTipsBot.Resources;
using OpenAI.ObjectModels.ResponseModels;
using Telegram.Bot.Exceptions;

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
        private readonly UserService _userService;

        public ChatGptHandler(
            MessageRepository messageRepository,
            IGpt gptService,
            UserStatusActivator typingStatus,
            ILogger<ChatGptHandler> log,
            ITelegramBotClient botClient,
            IAdvertisementClient gramadsAdvertisementClient,
            UserService userService)
        {
            _messageRepository = messageRepository;
            _gptService = gptService;
            _typingStatus = typingStatus;
            _log = log;
            _botClient = botClient;
            _advertisementClient = gramadsAdvertisementClient;
            _userService = userService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var shortMessage = update.Message.Text.Truncate(30) + "...";
            MessageDto gtpResponse = null;
            try
            {
                var profile = await _userService.GetUserProfile(update.UserChatKey.Id);

                if (profile is { GptRequests: <= 0, Stars: <= 0 })
                {
                    await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.NoFreeRequests,
                        replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
                    return;
                }

                var request = await _messageRepository.AddAsync(update.Message);
                var serviceMessageId = await _typingStatus.Start(update.UserChatKey, Telegram.Bot.Types.Enums.ChatAction.Typing);

                var sw = Stopwatch.StartNew();
                var token = MainHandler.UserState[update.UserChatKey].MessageIdToCancellation[serviceMessageId].Token;

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

                _log.LogInformation("Get response to promt '{promt}' takes {duration}s", shortMessage, sw.Elapsed.TotalSeconds);

                gtpResponse = new MessageDto(update.UserChatKey)
                {
                    Text = response.Choices.FirstOrDefault()?.Message.Content ?? "",
                    Role = Enums.MessageOwner.Assistant,
                    ContextBound = true,
                };

                await _messageRepository.AddAsync(gtpResponse, request);
                await _botClient.SendMarkdown2MessageAsync(update.UserChatKey.ChatId, gtpResponse.Text, (int)update.Message.TelegramMessageId!);

                await _userService.PayForGpt(update.UserChatKey.Id);

                if (profile is { GptRequests: <= 0, Stars: <= 0 })
                {
                    await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.NoFreeRequests,
                        replyMarkup: TelegramBotUiService.DepositInlineKeyboard);
                    return;
                }
            }
            catch (ClientException ex)
            {
                _log.LogInformation(ex, shortMessage);
                await _botClient.SendTextMessageAsync(update.UserChatKey.ChatId, ex.Message,
                    replyToMessageId: (int)update.Message.TelegramMessageId!);
                return;
            }
            catch (ApiRequestException ex)
            when (ex.Message.Contains("can't parse entities"))
            {
                var shortReply = gtpResponse!.Text.Truncate(30) + "...";
                _log.LogInformation(ex, "Telegram returns error while parsing markdown in message: {Reply}. Trying to resend without markdown",
                    shortReply);
                await _botClient.SendSplittedTextMessageAsync(update.UserChatKey.ChatId,
                    gtpResponse!.Text, replyToMessageId: (int)update.Message.TelegramMessageId!);
                return;
            }
            finally
            {
                await _typingStatus.Stop(update.UserChatKey);
            }

            await _advertisementClient.SendPostToChat(update.UserChatKey.ChatId);

            await base.HandleAsync(update);
        }
    }
}