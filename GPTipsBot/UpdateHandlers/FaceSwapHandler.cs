using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.InsightFaceSwap.FaceSwap;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class FaceSwapHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<FaceSwapHandler> logger;
        private readonly InsightFaceSwapper faceSwapper;
        public const int basedOnExperienceInputLengthLimit = 150;
        public static Dictionary<UserChatKey, List<Attachment>> userToSwapImages = new Dictionary<UserChatKey, List<Attachment>>();

        public FaceSwapHandler(ITelegramBotClient botClient, ILogger<FaceSwapHandler> logger, InsightFaceSwapper faceSwapper)
        {
            this.botClient = botClient;
            this.logger = logger;
            this.faceSwapper = faceSwapper;
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            if (userToSwapImages[update.UserChatKey] == null)
            {
                userToSwapImages[update.UserChatKey] = new List<Attachment>();
            }
            Stream destinationStream = null;
            var file = await botClient.GetInfoAndDownloadFileAsync(update.Message.Document.FileId, destinationStream, cancellationToken);
            var fileBytes = ReadFully(destinationStream);
            userToSwapImages[update.UserChatKey].Add(new Attachment(){ filename = update.Message.Document.FileName, file = fileBytes });

            if (userToSwapImages[update.UserChatKey].Count == 2)
            {
                faceSwapper.RunSwapJob(userToSwapImages[update.UserChatKey][0], userToSwapImages[update.UserChatKey][1]);

                while (true)
                {
                    var result = faceSwapper.GetResult();
                    if (result != null)
                    {
                        //send result
                        MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    }
                    await Task.Delay(2000);
                }
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }

        public byte[] ReadFully(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
