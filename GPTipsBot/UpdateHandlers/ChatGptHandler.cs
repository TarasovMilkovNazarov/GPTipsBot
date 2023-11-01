using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Telegram.Bot;
using GPTipsBot.Exceptions;
using GPTipsBot.Services;
using GPTipsBot.Resources;
using OpenAI.ObjectModels.ResponseModels;

namespace GPTipsBot.UpdateHandlers
{
    public class ChatGptHandler : BaseMessageHandler
    {
        private readonly MessageRepository messageRepository;
        private readonly IGpt gptService;
        private readonly ActionStatus typingStatus;
        private readonly ILogger<ChatGptHandler> log;
        private readonly ITelegramBotClient botClient;

        public ChatGptHandler(
            MessageRepository messageRepository,
            IGpt gptService,
            ActionStatus typingStatus,
            ILogger<ChatGptHandler> log,
            ITelegramBotClient botClient)
        {
            this.messageRepository = messageRepository;
            this.gptService = gptService;
            this.typingStatus = typingStatus;
            this.log = log;
            this.botClient = botClient;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var shortMessage = update.Message.Text.Truncate(30) + "...";
            try
            {
                update.ServiceMessage.TelegramMessageId = await typingStatus.Start(update.UserChatKey, Telegram.Bot.Types.Enums.ChatAction.Typing);

                var sw = Stopwatch.StartNew();
                var token = MainHandler.userState[update.UserChatKey].messageIdToCancellation[update.ServiceMessage.TelegramMessageId.Value].Token;

                ChatCompletionCreateResponse? response = null;

                try
                {
                    response = await gptService.SendMessage(update, token);
                }
                catch (OperationCanceledException)
                {
                    log.LogInformation("Request to openai service with promt '{promt}' was canceled", shortMessage);
                    return;
                }
                catch (ChatGptException ex)
                {
                    log.LogError("Failed request to OpenAi service: [{Code}] {Message}", response?.Error?.Code, response?.Error?.Message);
                    await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, BotResponse.SomethingWentWrong, (int)update.Message.TelegramMessageId!);

                    return;
                }
                finally
                {
                    sw.Stop();
                }

                log.LogInformation("Get response to promt '{promt}' takes {duration}s", shortMessage, sw.Elapsed.TotalSeconds);

                update.Reply.Text = response.Choices.FirstOrDefault()?.Message.Content ?? "";
                update.Reply.Role = Enums.MessageOwner.Assistant;
                update.Reply.ContextBound = true;
                messageRepository.AddMessage(update.Reply, update.Message.Id);
                await botClient.SendMarkdown2MessageAsync(update.UserChatKey.ChatId, update.Reply.Text, (int)update.Message.TelegramMessageId!);
            }
            catch (ClientException ex)
            {
                log.LogInformation(ex, shortMessage);
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, ex.Message, replyToMessageId: (int)update.Message.TelegramMessageId!);
                return;
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