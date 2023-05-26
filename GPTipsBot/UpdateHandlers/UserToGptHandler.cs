using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class UserToGptHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageContextRepository messageRepository;
        private readonly GptAPI gptAPI;
        private readonly ActionStatus typingStatus;
        private readonly ILogger<UserToGptHandler> logger;

        public UserToGptHandler(MessageHandlerFactory messageHandlerFactory, MessageContextRepository messageRepository,
            GptAPI gptAPI, ActionStatus typingStatus, ILogger<UserToGptHandler> logger)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.gptAPI = gptAPI;
            this.typingStatus = typingStatus;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<GptToUserHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.TelegramGptMessage;

            try
            {
                message.ServiceMessageId = await typingStatus.Start(message.UserKey, Telegram.Bot.Types.Enums.ChatAction.Typing, cancellationToken);
                Stopwatch sw = Stopwatch.StartNew();
                var token = MainHandler.userState[message.UserKey].messageIdToCancellation[message.ServiceMessageId].Token;
                var gtpResponse = await gptAPI.SendMessage(message, token);
                if (gtpResponse?.Error != null)
                {
                    throw new Exception(gtpResponse.Error.Message);
                }

                sw.Stop();
                logger.LogInformation($"Get response to message {message.MessageId} takes {sw.Elapsed.TotalSeconds}s");
                message.Reply = gtpResponse?.Choices?.FirstOrDefault()?.Message?.Content;
                messageRepository.AddBotResponse(message);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation("Request to openai service was canceled");
                SetNextHandler(null);
            }
            finally
            {
                await typingStatus.Stop(message.UserKey, cancellationToken);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
