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
        private string authCookie = "_U=19pYcXpVjNDL-xNgMtpQ81Td5QdEEf5jASLc-OjaFv-KEwuswa0R7-ZLuVd7Q_xt1VBHbK9IkgyPgbvaI4-8lRifA3tTvXo6TBDKVnQZErx-E9UDBrp-HeMtYrtV6BfXCBIe-BFkHKJ3v6-XSDXjuLa-0GpiAk95FOcNGU4w-FCzxblxIVJiP1BNhUltdSHrV5moIFdcM7hN0Ev9S5bBwfw;";
        public ImageCreatorService() {
            client = CreateBingRestClient();
            regex = new Regex(@"src=""([^""]+)""");
        }
        
        public void CreateImageFromText(string prompt)
        {
            var imgSrcs = GetImageSources(prompt);
            SaveImages(imgSrcs);
        }
        public byte[] GetImageFromText(string prompt)
        {
            var imgSrcs = GetImageSources(prompt);

            return GetImages(imgSrcs);
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

            client.AddDefaultHeader("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.AddDefaultHeader("accept-language", "en-US,en;q=0.9");
            client.AddDefaultHeader("cache-control", "max-age=0");
            client.AddDefaultHeader("content-type", "application/x-www-form-urlencoded");
            client.AddDefaultHeader("referrer", "https://www.bing.com/images/create/");
            client.AddDefaultHeader("origin", BING_URL);
            client.AddDefaultHeader("Cookie", authCookie);

            return client;
        }

        private List<string> GetImageSources(string prompt)
        {
            Console.WriteLine("Sending request...");
            string urlEncodedPrompt = Uri.EscapeDataString(prompt);
            // https://www.bing.com/images/create?q=<PROMPT>&rt=4&FORM=GENCRE
            string url = $"images/create?q={urlEncodedPrompt}&rt=4&FORM=GENCRE";
            var request = new RestRequest(url, Method.Post);

            var response = client.Execute(request);
            if (response.StatusCode != HttpStatusCode.Found)
            {
                Console.WriteLine($"ERROR: {response.Content}");
                throw new Exception("Redirect failed");
            }

            // Get redirect URL
            string redirectUrl = response.Headers.First(x => x.Name == "Location").Value.ToString();
            string requestId = redirectUrl.Split("id=")[^1];
            client.Execute(new RestRequest(redirectUrl, Method.Get));

            // https://www.bing.com/images/create/async/results/{ID}?q={PROMPT}
            string pollingUrl = $"images/create/async/results/{requestId}?q={urlEncodedPrompt}";
            // Poll for results
            response = client.ExecuteWithPredicate(new RestRequest(pollingUrl, Method.Get), IsImageSrcGetRequestSuccessfull);

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

        private byte[] GetImages(List<string> links)
        {
            Console.WriteLine("Getting images...");
            int imageNum = 0;
            byte[] image = null;

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
                        return response;
                    }
                }
                catch (Exception)
                {
                    // Error downloading file
                }
                imageNum++;
            }

            return image;
        }
    }
}
