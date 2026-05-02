using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Models;
using GPTipsBot.Services.YandexCloud;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorHandler(
        ITelegramBotClient botClient,
        ILogger<ImageGeneratorHandler> logger,
        IImageGenerator ya,
        UserStatusActivator sendImagestatus,
        ImageCreatorService imageCreatorService,
        MessageRepository messageRepository,
        IAdvertisementClient gramadsAdvertisementClient,
        UserService userService,
        ApplicationContext context,
        TelejetAdClient telejetAdClient,
        IJobService jobService,
        InMemoryAdvertisementTracker advertisementTracker,
        UserCommandRepository userCommandRepository)
        : BaseMessageHandler
    {
        private readonly ImageCreatorService _imageCreatorService = imageCreatorService;
        public const int ImageTextDescriptionLimit = 1000;
        public const int ImagesPerDayLimit = 5;

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var userKey = update.UserChatKey;

            if (update.Message.Text.Length > ImageTextDescriptionLimit)
            {
                await botClient.SendMessage(userKey.ChatId,
                    string.Format(BotResponse.ImageDescriptionLimitWarning, ImageTextDescriptionLimit),
                    replyMarkup: TelegramBotUiService.CancelKeyboard);
                return;
            }

            await using var dbTransaction = await context.Database.BeginTransactionAsync();
            var successPayment = await userService.PayForImageAsync(update.UserChatKey.Id);
            if (!successPayment)
            {
                await dbTransaction.RollbackAsync();
                context.ChangeTracker.Clear();

                var nextExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
                await botClient.SendOutOfFreeRequestsMessageAsync(update.UserChatKey.Id, nextExec);
                return;
            }

            var serviceMessageId = await sendImagestatus
                .Start(userKey, ChatAction.UploadPhoto);
            try
            {
                var sw = Stopwatch.StartNew();
                var token = Dispatcher.UserState[update.UserChatKey]
                    .MessageIdToCancellation[serviceMessageId].Token;

                var previousCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
                var isSquare = previousCommand?.Type == CommandType.ImageSquare;

                if (AppConfig.IsProduction)
                {
                    var response = await ya.GenerateImage(update.Message.Text,
                        isSquare);

                    using var imageStream = new MemoryStream(Convert.FromBase64String(response));
                    await messageRepository.AddAsync(new MessageDto(update.UserChatKey)
                    {
                        TelegramId = update.UserChatKey.Id,
                        Role = MessageOwner.Ya,
                        BotMessageType = BotMessageType.ImageGenerated,
                    });
                    await botClient.SendPhoto(userKey.ChatId, InputFile.FromStream(imageStream), cancellationToken: token);
                }
                else
                {
                    await messageRepository.AddAsync(new MessageDto(update.UserChatKey)
                    {
                        TelegramId = update.UserChatKey.Id,
                        Role = MessageOwner.Ya,
                        BotMessageType = BotMessageType.ImageGenerated,
                    });
                    await botClient.SendPhoto(userKey.ChatId, InputFile
                        .FromUri("https://www.kasandbox.org/programming-images/avatars/leaf-blue.png"),
                        cancellationToken: token);
                }

                await botClient.SendMessage(userKey.ChatId, string.Format(BotResponse.InputImageDescriptionText,
                        ImageTextDescriptionLimit),
                    replyMarkup: TelegramBotUiService.GetImageInstructionInlineKeyboard(isSquare), cancellationToken: token);

                sw.Stop();
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Image generation task was canceled");
            }
            catch (ClientException ex)
            {
                await botClient.SendMessage(userKey.ChatId, ex.Message, replyParameters: (int)update.Message.TelegramMessageId!);
            }
            catch (ImageCreatorException ex)
            {
                var statusCode = ex.Response?.StatusCode.ToString("G");

                logger.LogError(ex, $"Что-то пошло не так при получении ответа ({statusCode}) от создателя картинок.");

                await botClient.SendMessage(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            catch(Exception ex)
            {
                await botClient.SendMessage(userKey.ChatId, BotResponse.SomethingWentWrongWithImageService);
            }
            finally
            {
                await sendImagestatus.Stop(userKey);
            }

            await dbTransaction.CommitAsync();

            await gramadsAdvertisementClient.SendPostToChat(update.UserChatKey.ChatId);
            await telejetAdClient.SendToBapAsync(update.TelegramUpdate, "activity");
            await advertisementTracker.TrySendAdvertisement(update.UserChatKey.Id);
        }
    }
}
