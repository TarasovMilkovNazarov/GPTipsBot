using GPTipsBot.Extensions;
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
using OpenAI.ObjectModels.RequestModels;

namespace GPTipsBot.UpdateHandlers
{
    public class SummaryHandler : BaseMessageHandler
    {
        private const long MaxCharactersCount = 10000;
        private readonly MessageRepository messageRepository;
        private readonly IGpt gptService;
        private readonly ActionStatus typingStatus;
        private readonly ILogger<ChatGptHandler> log;
        private readonly ITelegramBotClient botClient;

        public SummaryHandler(
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
            try
            {
                var fileText = ReadFile();

                update.ServiceMessage.TelegramMessageId = await typingStatus.Start(update.UserChatKey, Telegram.Bot.Types.Enums.ChatAction.Typing);

                var sw = Stopwatch.StartNew();
                var token = MainHandler.userState[update.UserChatKey].messageIdToCancellation[update.ServiceMessage.TelegramMessageId.Value].Token;

                ChatCompletionCreateResponse? response = null;
                string currentSummary = "";

                try
                {
                    var systemPropmpt = new ChatMessage("system", "Сделай краткое резюме по тексту");
                    var lines = TextChunker.SplitPlainTextLines(fileText, 40);
                    var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 1000, chunkHeader: "DOCUMENT NAME: test.txt\n\n");

                    string[] summaries = new string[paragraphs.Count];
                    for (int i = 0; i < paragraphs.Count; i++)
                    {
                        string? paragraph = paragraphs[i];
                        var paragraphToResume = new ChatMessage("user", paragraph);
                        var summary = await gptService.SendMessageToChatGpt(new[] { systemPropmpt, paragraphToResume }, token);
                        summaries[i] = summary.Choices.FirstOrDefault()?.Message.Content;
                    }

                    currentSummary = summaries[0];
                    for (int i = 1; i < summaries.Length; i++)
                    {
                        var mergedSummaryWithText = currentSummary + "\r\n" + summaries[i];
                        var textToResume = new ChatMessage("user", mergedSummaryWithText);
                        var summaryResponse = await gptService.SendMessageToChatGpt(new[] { systemPropmpt, textToResume }, token);
                        currentSummary = summaryResponse.Choices.FirstOrDefault()?.Message.Content;
                    }
                }
                catch (OperationCanceledException)
                {
                    log.LogInformation("Summarization was canceled");
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

                log.LogInformation("Get response to summarization takes {duration}s", sw.Elapsed.TotalSeconds);

                update.Reply.Text = currentSummary;
                update.Reply.Role = Enums.MessageOwner.Assistant;
                update.Reply.ContextBound = true;
                messageRepository.AddMessage(update.Reply, update.Message.Id);
                await botClient.SendTextMessageAsync(update.UserChatKey.ChatId, update.Reply.Text, (int)update.Message.TelegramMessageId!);
            }
            catch (ClientException ex)
            {
                log.LogInformation(ex, null);
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

        private string ReadFile()
        {
            string filePath = "John Grey - Men Are from Mars Women Are from Venus.txt";
            var text = "";
            var numberOfCharacters = File.ReadAllLines(filePath).Sum(s => s.Length);
            if (numberOfCharacters > MaxCharactersCount)
            {
                throw new ClientException($"Too much characters in file. Please send file with less that {MaxCharactersCount}");
            }

            // Check if the file exists
            if (System.IO.File.Exists(filePath))
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        text += line;
                        Console.WriteLine(line);
                    }
                }
            }
            else
            {
                Console.WriteLine("File not found.");
            }

            return text;
        }
    }
}