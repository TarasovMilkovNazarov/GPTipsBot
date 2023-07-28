using GPTipsBot.Dtos.FaceSwap;
using RestSharp;

namespace GPTipsBot.InsightFaceSwap.Api
{
    public class DiscordMediaClient: RestClient
    {
        public DiscordMediaClient(): base("https://media.discordapp.net")
        {
            this.AddDefaultHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/115.0");
            this.AddDefaultHeader("Accept-Language", "en,en-US;q=0.5");
            this.AddDefaultHeader("Pragma", "no-cache");
            this.AddDefaultHeader("Cache-Control", "no-cache");
        }

        public async Task GetResult()
        {
            var request = new RestRequest($"attachments/{DiscordSettings.ChannelId}/1134140544122093610/nagiev_ins_ins.jpg");
            request.AddHeader("Accept", "image/avif,image/webp,*/*");
            request.AddHeader("Sec-Fetch-Dest", "image");
            request.AddHeader("Sec-Fetch-Mode", "no-cors");
            request.AddHeader("Sec-Fetch-Site", "cross-site");
            request.AddHeader("referrer", DiscordSettings.Host);

            var response = await this.ExecuteAsync(request);
        }
    }
}
