using GPTipsBot.Api;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class ChatGptHandler : BaseMessageHandler
    {
        private readonly MessageRepository messageRepository;
        private readonly IGpt gptApi;
        private readonly ActionStatus typingStatus;
        private readonly ILogger<ChatGptHandler> logger;
        private readonly ITelegramBotClient botClient;

        public ChatGptHandler(MessageRepository messageRepository, IGpt gptApi, ActionStatus typingStatus, ILogger<ChatGptHandler> logger, ITelegramBotClient botClient)
        {
            this.messageRepository = messageRepository;
            this.gptApi = gptApi;
            this.typingStatus = typingStatus;
            this.logger = logger;
            this.botClient = botClient;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var shotMessage = update.Message.Text.Truncate(30) + "...";
            try
            {
                update.ServiceMessage.TelegramMessageId = await typingStatus.Start(update.UserChatKey, Telegram.Bot.Types.Enums.ChatAction.Typing);
                
                var sw = Stopwatch.StartNew();
                var token = MainHandler.userState[update.UserChatKey].messageIdToCancellation[update.ServiceMessage.TelegramMessageId.Value].Token;
                var gtpResponse = await gptApi.SendMessage(update, token);
                if (gtpResponse.Error != null)
                    throw new Exception(JsonConvert.SerializeObject(gtpResponse.Error));
                sw.Stop();
                
                logger.LogInformation("Get response to promt '{promt}' takes {duration}s", shotMessage, sw.Elapsed.TotalSeconds);
                
                update.Reply.Text = gtpResponse.Choices.FirstOrDefault()?.Message.Content ?? "";
                update.Reply.Role = Enums.MessageOwner.Assistant;
                update.Reply.ContextBound = true;
                messageRepository.AddMessage(update.Reply, update.Message.Id);
                await botClient.SendMarkdown2MessageAsync(update.UserChatKey.ChatId, update.Reply.Text, (int)update.Message.TelegramMessageId!);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Request to openai service with promt '{promt}' was canceled", shotMessage);
                SetNextHandler(null);
            }
            finally
            {
                await typingStatus.Stop(update.UserChatKey);
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
