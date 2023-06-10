using GPTipsBot.Api;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class ChatGptHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageRepository messageRepository;
        private readonly GptAPI gptAPI;
        private readonly ActionStatus typingStatus;
        private readonly ILogger<ChatGptHandler> logger;
        private readonly ITelegramBotClient botClient;

        public ChatGptHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageRepository,
            GptAPI gptAPI, ActionStatus typingStatus, ILogger<ChatGptHandler> logger, ITelegramBotClient botClient)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageRepository = messageRepository;
            this.gptAPI = gptAPI;
            this.typingStatus = typingStatus;
            this.logger = logger;
            this.botClient = botClient;
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            try
            {
                update.ServiceMessage.TelegramMessageId = await typingStatus.Start(update.UserChatKey, Telegram.Bot.Types.Enums.ChatAction.Typing, cancellationToken);
                Stopwatch sw = Stopwatch.StartNew();
                var token = MainHandler.userState[update.UserChatKey].messageIdToCancellation[update.ServiceMessage.TelegramMessageId.Value].Token;
                var gtpResponse = await gptAPI.SendMessage(update, token);
                if (gtpResponse?.Error != null)
                {
                    throw new Exception(gtpResponse.Error.Message);
                }

                sw.Stop();
                logger.LogInformation($"Get response to message {update.Message.Id} takes {sw.Elapsed.TotalSeconds}s");
                update.Reply.Text = gtpResponse?.Choices?.FirstOrDefault()?.Message?.Content;
                messageRepository.AddMessage(update.Reply, update.Message.Id);
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, update.Reply.Text, replyToMessageId: (int)update.Message.TelegramMessageId);
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation("Request to openai service was canceled");
                SetNextHandler(null);
            }
            finally
            {
                await typingStatus.Stop(update.UserChatKey, cancellationToken);
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
