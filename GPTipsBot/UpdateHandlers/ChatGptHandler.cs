using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using Telegram.Bot;
using GPTipsBot.Exceptions;
using GPTipsBot.Jobs;
using GPTipsBot.Services;
using GPTipsBot.Resources;
using OpenAI.ObjectModels.ResponseModels;

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
        IJobService jobService)
        : BaseMessageHandler
    {
        public override async Task HandleAsync(UpdateDecorator update)
        {
            var shortMessage = update.Message.Text.Truncate(30) + "...";
            var chatId = update.UserChatKey.Id;

            await using var dbTransaction = await context.Database.BeginTransactionAsync();
            var request = await messageRepository.AddAsync(update.Message);
            var successPayment = await userService.PayForGpt(chatId);

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
                var serviceMessageId = await typingStatus.Start(update.UserChatKey, Telegram.Bot.Types.Enums.ChatAction.Typing);

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
                catch (ChatGptException ex)
                {
                    log.LogError("Failed request to OpenAi service: [{Code}] {Message}", response?.Error?.Code, response?.Error?.Message);
                    await botClient.SendMessage(
                        chatId,
                        BotResponse.SomethingWentWrong,
                        replyParameters: (int)update.Message.TelegramMessageId, cancellationToken: token
                        );

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
                await botClient.TrySendMarkdown2MessageAsync(chatId, gptResponse.Text, (int)update.Message.TelegramMessageId);
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
            await gramadsAdvertisementClient.SendPostToChat(chatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
        }
    }
}