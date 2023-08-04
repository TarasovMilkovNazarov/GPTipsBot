using GPTipsBot.InsightFaceSwap.FaceSwap;
using Newtonsoft.Json;
using RestSharp;

namespace GPTipsBot.InsightFaceSwap.Api
{
    public class DiscordRestClient: RestClient
    {
        public DiscordRestClient(): base("https://discord.com/api/v9/")
        {
            this.AddDefaultHeader("Accept", "*/*");
            this.AddDefaultHeader("Accept-Language", "en,en-US;q=0.5");
            this.AddDefaultHeader("Authorization", DiscordSettings.Auth);
            this.AddDefaultHeader("Cache-Control", "no-cache");
            this.AddDefaultHeader("Cookie", "__dcfduid=3c94c1fae35911ed9d9bc21897fa2a0c; __sdcfduid=3c94c1fae35911ed9d9bc21897fa2a0c83c43e7abd1bd4f43f098838b5e44b805b2d4da2aba30c1c0e2043786ebe8f40; OptanonConsent=isIABGlobal=false&datestamp=Fri+Jul+28+2023+10%3A32%3A35+GMT%2B0500+(Yekaterinburg+Standard+Time)&version=6.33.0&hosts=&landingPath=https%3A%2F%2Fdiscord.com%2F&groups=C0001%3A1%2CC0002%3A1%2CC0003%3A1; _ga=GA1.1.963069424.1683113346; __cfruid=6659c558d24674b110e9ea47e1b9f281536782f2-1690455515; cf_clearance=NclBheP8Ap.MEIIMk4d6zG_BG7EItZyg54Ui_tBVquQ-1690467193-0-0.2.1690467193; locale=en-GB; _gid=GA1.2.1011648386.1690465093; _gcl_au=1.1.1161220991.1690522355; _ga_Q149DFWHT7=GS1.1.1690522355.1.0.1690522359.0.0.0");
            this.AddDefaultHeader("Pragma", "no-cache");
            this.AddDefaultHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/115.0");
            this.AddDefaultHeader("X-Debug-Options", "bugReporterEnabled");
            this.AddDefaultHeader("X-Discord-Locale", "en-GB");
            this.AddDefaultHeader("X-Discord-Timezone", "Asia/Yekaterinburg");
        }

        public void SendInteraction(Interaction data)
        {
            var request = new RestRequest("interactions", Method.Post);
            request.AddHeader("Content-Type", "multipart/form-data");
            request.AddHeader("Sec-Fetch-Dest", "empty");
            request.AddHeader("Sec-Fetch-Mode", "cors");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            //request.AddBody(JsonConvert.SerializeObject(data));
            request.AddParameter("payload_json", JsonConvert.SerializeObject(data), ParameterType.RequestBody);

            var response = this.Execute(request);
        }

        public UploadResult GetUploadUrlToStorage(Upload files)
        {
            var payload = JsonConvert.SerializeObject(files);

            var request = new RestRequest($"channels/{DiscordSettings.ChannelId}/attachments", Method.Post);
            request.Timeout = -1;
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("X-Super-Properties", "eyJvcyI6IldpbmRvd3MiLCJicm93c2VyIjoiRmlyZWZveCIsImRldmljZSI6IiIsInN5c3RlbV9sb2NhbGUiOiJlbiIsImJyb3dzZXJfdXNlcl9hZ2VudCI6Ik1vemlsbGEvNS4wIChXaW5kb3dzIE5UIDEwLjA7IFdpbjY0OyB4NjQ7IHJ2OjEwOS4wKSBHZWNrby8yMDEwMDEwMSBGaXJlZm94LzExNS4wIiwiYnJvd3Nlcl92ZXJzaW9uIjoiMTE1LjAiLCJvc192ZXJzaW9uIjoiMTAiLCJyZWZlcnJlciI6Imh0dHBzOi8vd3d3Lmdvb2dsZS5jb20vIiwicmVmZXJyaW5nX2RvbWFpbiI6Ind3dy5nb29nbGUuY29tIiwic2VhcmNoX2VuZ2luZSI6Imdvb2dsZSIsInJlZmVycmVyX2N1cnJlbnQiOiJodHRwczovL3d3dy5nb29nbGUuY29tLyIsInJlZmVycmluZ19kb21haW5fY3VycmVudCI6Ind3dy5nb29nbGUuY29tIiwic2VhcmNoX2VuZ2luZV9jdXJyZW50IjoiZ29vZ2xlIiwicmVsZWFzZV9jaGFubmVsIjoic3RhYmxlIiwiY2xpZW50X2J1aWxkX251bWJlciI6MjE1NTI3LCJjbGllbnRfZXZlbnRfc291cmNlIjpudWxsfQ==");
            request.AddHeader("Sec-Fetch-Dest", "empty");
            request.AddHeader("Sec-Fetch-Mode", "cors");
            request.AddHeader("Sec-Fetch-Site", "same-origin");
            request.AddHeader("Referrer", $"https://discord.com/channels/{DiscordSettings.Guild}/{DiscordSettings.ChannelId}");
            request.AddBody(payload, ContentType.Json);
            var response = this.Execute(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ErrorMessage);
            }

            return JsonConvert.DeserializeObject<UploadResult>(response.Content);
        }
    }
}
