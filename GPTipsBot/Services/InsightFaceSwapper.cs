using GPTipsBot.Dtos.FaceSwap;
using GPTipsBot.InsightFaceSwap.Api;
using System.Text;

namespace GPTipsBot.Services
{
    public class InsightFaceSwapper
    {
        public InsightFaceSwapper() { }

        public void RunSwapJob(Attachment identityImage, Attachment targetImage)
        {
            var discordClient = new DiscordRestClient();
            var storageClient = new StorageClient();
            
            var identity = new DiscordFile()
            {
                filename = identityImage.filename,
                file_size = 1024,
                id = "1",
                is_clip = false
            };

            var targetFile = new DiscordFile()
            {
                filename = targetImage.filename,
                file_size = 1024,
                id = "2",
                is_clip = false
            };

            var uploadResult = discordClient.GetUploadUrlToStorage(new Upload() { files = new[] { targetFile, identity } });
            storageClient.UploadImage(identityImage.file, uploadResult.attachments[0].upload_url.Substring("https://discord-attachments-uploads-prd.storage.googleapis.com/".Length));
            storageClient.UploadImage(targetImage.file, uploadResult.attachments[1].upload_url.Substring("https://discord-attachments-uploads-prd.storage.googleapis.com/".Length));
            var sessionId = GenerateRandomSessionId("a1acca8520d36d6e5ede8770e9a16066".Length);

            var saveIdData = new Interaction()
            {
                application_id = DiscordSettings.InsightSwapFaceApplicationId,
                type = (int)Discord.InteractionType.ApplicationCommand,
                guild_id = DiscordSettings.ServerId,
                channel_id = DiscordSettings.ChannelId,
                session_id = sessionId,
                data = new Data()
                {
                    id = "1097018209481261127",
                    version = "1097018209481261130",
                    name = "saveid",
                    type = 1,
                    options = new List<Option>() { 
                        new Option() { type = Discord.ApplicationCommandOptionType.String, name = "idname", value = "buziev" }, 
                        new Option() { type = Discord.ApplicationCommandOptionType.Attachment, name = "image", value = "0" }
                    },
                    attachments = new Attachment[]
                    {
                        new Attachment()
                        {
                            id="0",
                            filename = identityImage.filename,
                            uploaded_filename = uploadResult.attachments[0].upload_filename,
                        }
                    },
                    application_command = new SaveIdApplicationCommand()
                },
            };
            discordClient.SendInteraction(saveIdData);

            var swapData = new Interaction()
            {
                application_id = DiscordSettings.InsightSwapFaceApplicationId,
                type = (int)Discord.InteractionType.ApplicationCommand,
                guild_id = DiscordSettings.ServerId,
                channel_id = DiscordSettings.ChannelId,
                session_id = sessionId,
                data = new Data()
                {
                    version = "1097030226204184647",
                    id = "1097030226204184646",
                    name = "swapid",
                    type = 1,
                    options = new List<Option>() { 
                        new Option() { type = Discord.ApplicationCommandOptionType.String, name = "idname", value = "buziev" }, 
                        new Option() { type = Discord.ApplicationCommandOptionType.Attachment, name = "image", value = "0" }
                    },
                    attachments = new Attachment[]
                    {
                        new Attachment()
                        {
                            id="0",
                            filename = targetImage.filename,
                            uploaded_filename = uploadResult.attachments[1].upload_filename,
                        }
                    },
                    application_command = new SwapApplicationCommand()
                },
            };

            discordClient.SendInteraction(swapData);
        }

        public byte[] GetResult()
        {
            throw new NotImplementedException();
        }

        string GenerateRandomSessionId(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random(); 
            var sessionId = new StringBuilder(length);

            while (sessionId.Length < length)
            {
                sessionId.Append(chars[random.Next(chars.Length)]);
            }

            return sessionId.ToString();
        }
    }
}
