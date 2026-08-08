using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.ResponseModels;
using System.Diagnostics;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class ChatGptHandler(
        MessageRepository messageRepository,
        IGpt gptService,
        UserStatusActivator typingStatus,
        ILogger<ChatGptHandler> log,
        ITelegramBotClient botClient,
        IAdvertisementClient gramadsAdvertisementClient,
        TelejetAdClient telejetAdClient,
        UserService userService,
        ApplicationContext context,
        IJobService jobService,
        InMemoryAdvertisementTracker advertisementTracker)
        : BaseMessageHandler
    {
        public override async Task HandleAsync(UpdateDecorator update)
        {
            var shortMessage = update.Message.Text.Truncate(30) + "...";
            var chatId = update.UserChatKey.ChatId;
            var userId = update.UserChatKey.Id;

            await using var dbTransaction = await context.Database.BeginTransactionAsync();
            var request = await messageRepository.AddAsync(update.Message);
            var successPayment = await userService.PayForGpt(userId);

            if (!successPayment)
            {
                await dbTransaction.RollbackAsync();
                context.ChangeTracker.Clear();
                var nextExecution = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                await botClient.SendOutOfFreeRequestsMessageAsync(chatId, nextExecution);
                return;
            }

            Guard.Against.Null(update.Message.TelegramMessageId);

            try
            {
                var serviceMessageId = await typingStatus.Start(
                    update.UserChatKey,
                    Telegram.Bot.Types.Enums.ChatAction.Typing,
                    chatId);

                var sw = Stopwatch.StartNew();
                var token = Dispatcher.UserState[update.UserChatKey].MessageIdToCancellation[serviceMessageId].Token;

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
                catch (ChatGptException)
                {
                    log.LogError("Failed request to OpenAi service: [{Code}] {Message}", response?.Error?.Code, response?.Error?.Message);
                    await botClient.SendMessage(
                        chatId,
                        BotResponse.SomethingWentWrong,
                        replyParameters: (int)update.Message.TelegramMessageId,
                        cancellationToken: token);
                    return;
                }
                finally
                {
                    sw.Stop();
                }

                log.LogInformation("Get response to prompt '{prompt}' takes {duration}s", shortMessage, sw.Elapsed.TotalSeconds);

                var gptResponse = new MessageDto(update.UserChatKey)
                {
                    Text = response.Choices.FirstOrDefault()?.Message.Content ?? "",
                    Role = Enums.MessageOwner.Assistant,
                    ContextBound = true,
                };

                Guard.Against.Null(gptResponse);

                await messageRepository.AddAsync(gptResponse, request);
                await botClient.TrySendMarkdown2MessageAsync(
                    chatId,
                    gptResponse.Text,
                    (int)update.Message.TelegramMessageId,
                    logger: log);
            }
            catch (ClientException ex)
            {
                log.LogInformation(ex, shortMessage);
                await botClient.SendMessage(chatId, ex.Message,
                    replyParameters: (int)update.Message.TelegramMessageId);
                return;
            }
            finally
            {
                await typingStatus.Stop(update.UserChatKey);
            }

            await dbTransaction.CommitAsync();

            if (update.IsGroupOrChannel)
            {
                return;
            }

            await gramadsAdvertisementClient.SendPostToChat(chatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
            await advertisementTracker.TrySendAdvertisement(userId);
        }
    }
}
