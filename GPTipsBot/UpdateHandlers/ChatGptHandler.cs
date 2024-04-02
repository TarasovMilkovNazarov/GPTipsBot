﻿using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Telegram.Bot;
using GPTipsBot.Exceptions;
using GPTipsBot.Services;
using GPTipsBot.Resources;
using OpenAI.ObjectModels.ResponseModels;
using Telegram.Bot.Exceptions;
using Microsoft.SemanticKernel.Text;
using static System.Net.Mime.MediaTypeNames;
using OpenAI.ObjectModels.RequestModels;

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
                string currentSummary = "";

                try
                {
                    var lines = TextChunker.SplitPlainTextLines(update.Message.Text, 40);
                    var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 1000, chunkHeader: "DOCUMENT NAME: test.txt\n\n");

                    string[] summaries = new string[paragraphs.Count];
                    for (int i = 0; i < paragraphs.Count; i++)
                    {
                        string? paragraph = paragraphs[i];
                        var systemPropmpt = new ChatMessage("system", "Сделай краткое резюме по тексту");
                        var paragraphToResume = new ChatMessage("user", paragraph);
                        var summary = await gptService.SendMessageToChatGpt(new[] { systemPropmpt, paragraphToResume }, token);
                        summaries[i] = summary.Choices.FirstOrDefault()?.Message.Content;
                    }

                    currentSummary = summaries[0];
                    for (int i = 1; i < summaries.Length; i++)
                    {
                        var systemPropmpt = new ChatMessage("system", "Сделай краткое резюме по тексту");
                        var mergedSummaryWithText = currentSummary + "\r\n" + summaries[i];
                        var textToResume = new ChatMessage("user", mergedSummaryWithText);
                        var summaryResponse = await gptService.SendMessageToChatGpt(new[] { systemPropmpt, textToResume }, token);
                        currentSummary = summaryResponse.Choices.FirstOrDefault()?.Message.Content;
                    }
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

                update.Reply.Text = currentSummary;
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
            catch (ApiRequestException ex)
            when (ex.Message.Contains("can't parse entities"))
            {
                var shortReply = update.Reply.Text.Truncate(30) + "...";
                log.LogInformation(ex, "Telegram returns error while parsing markdown in message: {Reply}. Trying to resend without markdown", shortReply);
                await botClient.SendSplittedTextMessageAsync(update.UserChatKey.ChatId, update.Reply.Text, replyToMessageId: (int)update.Message.TelegramMessageId!);
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