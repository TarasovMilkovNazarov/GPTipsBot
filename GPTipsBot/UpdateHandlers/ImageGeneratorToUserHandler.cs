using GPTipsBot.Api;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class ImageGeneratorToUserHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<ImageGeneratorToUserHandler> logger;
        private readonly ImageService imageService;
        private readonly ActionStatus sendImagestatus;

        public ImageGeneratorToUserHandler(ITelegramBotClient botClient, ILogger<ImageGeneratorToUserHandler> logger,
            ImageService imageService, ActionStatus sendImagestatus)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.imageService = imageService;
            this.sendImagestatus = sendImagestatus;
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.TelegramGptMessage;
            var chatId = update.Update.Message.Chat.Id;

            await sendImagestatus.Start(chatId, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto, cancellationToken);
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                await imageService.SendImageToTelegramUser(message.ChatId, update.Update.Message.Text);
                sw.Stop();
                logger.LogInformation($"Successfull image generation for message {message.MessageId} takes {sw.Elapsed.TotalSeconds}s");
            }
            catch(Exception ex)
            {
                await botClient.SendTextMessageAsync(chatId, BotResponse.SomethingWentWrongWithImageService, cancellationToken: cancellationToken);
            }
            finally
            {
                MainHandler.userState[update.TelegramGptMessage.TelegramId] = Enums.UserStateEnum.None;
                await sendImagestatus.Stop(cancellationToken);
            }
            

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
