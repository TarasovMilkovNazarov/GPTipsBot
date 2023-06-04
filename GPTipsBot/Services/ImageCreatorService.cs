using GPTipsBot.Api;
using GPTipsBot.Extensions;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Telegram.Bot.Requests.Abstractions;

namespace GPTipsBot.Services
{
    public class ImageCreatorService
    {
        private string BING_URL = "https://www.bing.com";
        private readonly RestClient client;
        private readonly Regex regex;
        private string authCookie = "_U=1w4pYzmPHOt2iUw36dqGgIqoHTw9Gi13APgy9f0K5FnXL41xpEcj3BIa_SpOVOK_Vgyp9zqDVRPnalY75qdUu5S1L8lXw65Y6-UB0h9xl7nj5oeQcXbpV_ZQ1V7DKb_0oCWNUWxLZ9ckJDWqNioW_5E9C3pEARq-NxSTw250AIvL0EWn5FXFXGr1xtSLGIZ6ZaXNF8KvHppMyqbtlS2PXZQ";
        public ImageCreatorService() {
            client = CreateBingRestClient();
            regex = new Regex(@"src=""([^""]+)""");

            //uncomment for sniffing requests in fiddler
            //ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }
        
        public async Task CreateImageFromText(string prompt, CancellationToken token = default)
        {
            var imgSrcs = await GetImageSources(prompt, token);
            SaveImages(imgSrcs);
        }

        private RestClient CreateBingRestClient()
        {
            var cookie = new Cookie("_U", HttpUtility.UrlEncode(authCookie))
            {
                Domain = "www.bing.com"
            };

            var cookieContainer = new CookieContainer() { MaxCookieSize = int.MaxValue };
            cookieContainer.Add(cookie);

            var client = new RestClient(new RestClientOptions(BING_URL) {
                FollowRedirects = false,
                UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36 Edg/110.0.1587.63",
                //CookieContainer = cookieContainer
            });

            Random random = new Random();
        
            // Generate random IP between range 13.104.0.0/14
            string FORWARDED_IP = $"13.{random.Next(104, 108)}.{random.Next(0, 256)}.{random.Next(0, 256)}";

            client.AddDefaultHeader("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.AddDefaultHeader("accept-language", "en-US,en;q=0.9");
            client.AddDefaultHeader("cache-control", "max-age=0");
            client.AddDefaultHeader("content-type", "application/x-www-form-urlencoded");
            client.AddDefaultHeader("referrer", "https://www.bing.com/images/create/");
            client.AddDefaultHeader("origin", BING_URL);
            client.AddDefaultHeader("Cookie", authCookie);
            client.AddDefaultHeader("x-forwarded-for", FORWARDED_IP);

            return client;
        }

        public async Task<List<string>> GetImageSources(string prompt, CancellationToken token)
        {
            Console.WriteLine("Sending request...");
            string urlEncodedPrompt = Uri.EscapeDataString(prompt);
            var payload = $"q={urlEncodedPrompt}&qs=ds";

            // https://www.bing.com/images/create?q=<PROMPT>&rt=4&FORM=GENCRE
            string url = $"images/create?q={urlEncodedPrompt}&rt=4&FORM=GENCRE";
            var request = new RestRequest(url, Method.Post);
            request.AddHeader("Accept-Encoding", "identity");
            request.AddParameter($"q", urlEncodedPrompt);
            request.AddParameter($"qs", "ds");
            request.Timeout = 2000;
             
            var response = await client.ExecuteAsync(request, token);

            if (string.IsNullOrEmpty(response?.Content))
            {
                throw new CustomException(BotResponse.SomethingWentWrongWithImageService);
            }

            if (response.Content.ToLower().Contains("this prompt has been blocked"))
            {
                throw new CustomException(BingImageCreatorResponse.BlockedPromptError);
            }
            if (response.Content.ToLower().Contains("we're working hard to offer image creator in more languages"))
            {
                throw new CustomException(BingImageCreatorResponse.UnsupportedLangError);
            }

            // Get redirect URL
            string redirectUrl = response.Headers.First(x => x.Name == "Location").Value.ToString();
            string requestId = redirectUrl.Split("id=")[^1];
            await client.ExecuteAsync(new RestRequest(redirectUrl, Method.Get), token);

            // https://www.bing.com/images/create/async/results/{ID}?q={PROMPT}
            string pollingUrl = $"images/create/async/results/{requestId}?q={urlEncodedPrompt}";
            // Poll for results
            response = await client.ExecuteWithPredicate(new RestRequest(pollingUrl, Method.Get), token, IsImageSrcGetRequestSuccessfull);

            // Use regex to search for src=""
            var imageLinks = new List<string>();
            foreach (Match match in regex.Matches(response.Content))
            {
                imageLinks.Add(match.Groups[1].Value);
            }
            // Remove duplicates
            return new List<string>(new HashSet<string>(imageLinks));
        }

        private bool IsImageSrcGetRequestSuccessfull(RestResponse response, int currentAttempt)
        {
            if (currentAttempt == 0)
            {
                return false;
            }

            var maxRetries = 100;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception("Could not get results");
            }
            if (!string.IsNullOrEmpty(response.Content))
            {
                return true;
            }

            if (currentAttempt < maxRetries)
            {
                Thread.Sleep(1000);
                return false;
            }

            return true;
        }

        private void SaveImages(List<string> links, string outputDir = "output")
        {
            Console.WriteLine("\nDownloading images...");
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (IOException)
            {
                // Directory already exists
            }
            int imageNum = 0;

            var client = new RestClient();
            foreach (string link in links)
            {
                try
                {
                    var request = new RestRequest(link);
                    request.AddHeader("Accept", "image/jpeg");
                    var response = client.DownloadData(request);
                    File.WriteAllBytes($"{outputDir}/{imageNum}.jpeg", response);
                }
                catch (Exception)
                {
                    // Error downloading file
                }
                imageNum++;
            }
        }

        public List<byte[]> GetImages(List<string> links)
        {
            var images = new List<byte[]>();
            Console.WriteLine("Getting images...");

            var client = new RestClient();
            foreach (string link in links)
            {
                try
                {
                    var request = new RestRequest(link);
                    request.AddHeader("Accept", "image/jpeg");
                    var response = client.DownloadData(request);
                    if (response != null)
                    {
                        images.Add(response);
                    }
                }
                catch (Exception)
                {
                    // Error downloading file
                }
            }

            return images;
        }

        public byte[] GetImage(string link)
        {
            Console.WriteLine("Getting images...");
            byte[]? image = null;

            try
            {
                var request = new RestRequest(link);
                request.AddHeader("Accept", "image/jpeg");
                image = client.DownloadData(request);
                if (image == null)
                {
                    throw new Exception($"Can't get image from source {link}");
                }
            }
            catch (Exception)
            {
                // Error downloading file
            }

            return image;
        }
    }
}
