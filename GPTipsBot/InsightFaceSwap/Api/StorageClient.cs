using GPTipsBot.InsightFaceSwap.FaceSwap;
using RestSharp;

namespace GPTipsBot.InsightFaceSwap.Api
{
    public class StorageClient: RestClient
    {
        public StorageClient(): base("https://discord-attachments-uploads-prd.storage.googleapis.com")
        {
            this.AddDefaultHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 Firefox/115.0");
            this.AddDefaultHeader("Accept-Language", "en,en-US;q=0.5");
            this.AddDefaultHeader("Pragma", "no-cache");
            this.AddDefaultHeader("Cache-Control", "no-cache");
            this.AddDefaultHeader("X-Super-Properties", "eyJvcyI6IldpbmRvd3MiLCJicm93c2VyIjoiRmlyZWZveCIsImRldmljZSI6IiIsInN5c3RlbV9sb2NhbGUiOiJlbiIsImJyb3dzZXJfdXNlcl9hZ2VudCI6Ik1vemlsbGEvNS4wIChXaW5kb3dzIE5UIDEwLjA7IFdpbjY0OyB4NjQ7IHJ2OjEwOS4wKSBHZWNrby8yMDEwMDEwMSBGaXJlZm94LzExNS4wIiwiYnJvd3Nlcl92ZXJzaW9uIjoiMTE1LjAiLCJvc192ZXJzaW9uIjoiMTAiLCJyZWZlcnJlciI6Imh0dHBzOi8vd3d3Lmdvb2dsZS5jb20vIiwicmVmZXJyaW5nX2RvbWFpbiI6Ind3dy5nb29nbGUuY29tIiwic2VhcmNoX2VuZ2luZSI6Imdvb2dsZSIsInJlZmVycmVyX2N1cnJlbnQiOiJodHRwczovL3d3dy5nb29nbGUuY29tLyIsInJlZmVycmluZ19kb21haW5fY3VycmVudCI6Ind3dy5nb29nbGUuY29tIiwic2VhcmNoX2VuZ2luZV9jdXJyZW50IjoiZ29vZ2xlIiwicmVsZWFzZV9jaGFubmVsIjoic3RhYmxlIiwiY2xpZW50X2J1aWxkX251bWJlciI6MjE1NTI3LCJjbGllbnRfZXZlbnRfc291cmNlIjpudWxsfQ==");
        }

        public void UploadImage(byte[] image, string uploadUrl)
        {
            var request = new RestRequest(uploadUrl, Method.Put);
            request.AddHeader("Accept", "*/*");
            request.AddHeader("Content-Type", "application/octet-stream");
            request.AddHeader("Sec-Fetch-Dest", "empty");
            request.AddHeader("Sec-Fetch-Mode", "cors");
            request.AddHeader("Sec-Fetch-Site", "cross-site");
            request.AddHeader("Referrer", DiscordSettings.Host);
            request.AddHeader("mode", "cors");

            request.AddBody(image, ContentType.Binary);

            var response = this.Execute(request);
        }
    }
}
