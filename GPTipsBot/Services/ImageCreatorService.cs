using GPTipsBot.Api;
using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using RestSharp;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Services
{
    public class ImageCreatorService
    {
        private readonly ILogger<ImageCreatorService> log;
        private readonly string BING_URL = "https://www.bing.com";
        private readonly RestClient client;
        private readonly Regex regex;
        private string authCookie = "_U=1456EW-a6Az8mMPrb16LGlUdXHoW9vvDSqJshkOX3RxDwKitZsomgd1gOdMR_XUaPzuskHy0LntA9xFn2_zFim8HOS_Ej9NYZPNvuD6PyhnlD7q8RmnW3bV93kU9PspJoFbKRsP3AnEG8BdF3GevJg2qaQlufUOcjmNKwhccHXejsHX893adP1EybSy79kOEDizLmFa0SUzmcTBygkaxCTA;";
        public ImageCreatorService(ILogger<ImageCreatorService> log) {
            this.log = log;
            client = CreateBingRestClient();
            regex = new Regex(@"src=""([^""]+)""");

            //uncomment for sniffing requests in fiddler
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }

        private RestClient CreateBingRestClient()
        {
            var newClient = new RestClient(new RestClientOptions(BING_URL) {
                FollowRedirects = false,
                UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36 Edg/110.0.1587.63",
            });

            Random random = new Random();
        
            // Generate random IP between range 13.104.0.0/14
            string FORWARDED_IP = $"13.{random.Next(104, 108)}.{random.Next(0, 256)}.{random.Next(0, 256)}";

            newClient.AddDefaultHeader("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            newClient.AddDefaultHeader("accept-language", "en-US,en;q=0.9");
            newClient.AddDefaultHeader("cache-control", "max-age=0");
            newClient.AddDefaultHeader("content-type", "application/x-www-form-urlencoded");
            newClient.AddDefaultHeader("referrer", $"https://www.bing.com/images/create/");
            newClient.AddDefaultHeader("origin", BING_URL);
            newClient.AddDefaultHeader("x-forwarded-for", FORWARDED_IP);
            newClient.AddDefaultHeader("Cookie", authCookie);

            return newClient;
        }

        public async Task<List<string>> GetImageSources(string prompt, CancellationToken token)
        {
            log.LogInformation("Send request to Bing Image Creator service: {prompt}", prompt);
            
            string urlEncodedPrompt = Uri.EscapeDataString(prompt);

            // https://www.bing.com/images/create?q=<PROMPT>&rt=4&FORM=GENCRE
            string url = $"images/create?q={urlEncodedPrompt}&rt=4&FORM=GENCRE";
            var request = new RestRequest(url, Method.Post);
            request.AddHeader("Accept-Encoding", "identity");
            request.AddParameter("q", urlEncodedPrompt);
            request.AddParameter("qs", "ds");
            request.Timeout = 10000;

            var response = await client.ExecuteAsync(request, token);

            try
            {
                if (string.IsNullOrEmpty(response?.Content))
                {
                    throw new ClientException(BotResponse.SomethingWentWrongWithImageService);
                }

                if (response.Content.ToLower().Contains("this prompt has been blocked"))
                {
                    throw new ClientException(BingResponse.BlockedPromptError);
                }
                if (response.Content.ToLower().Contains("we're working hard to offer image creator in more languages"))
                {
                    throw new ClientException(BingResponse.UnsupportedLangError);
                }

                // Get redirect URL
                string? redirectUrl = response.Headers?.First(x => x.Name == "Location").Value?.ToString()?.Replace("&nfy=1", "");
                string requestId = redirectUrl.Split("id=")[^1];
                await client.ExecuteAsync(new RestRequest(redirectUrl, Method.Get), token);

                // https://www.bing.com/images/create/async/results/{ID}?q={PROMPT}
                string pollingUrl = $"images/create/async/results/{requestId}?q={urlEncodedPrompt}";
                // Poll for results
                var getImagesLinksRequest = new RestRequest(pollingUrl, Method.Get);

                response = await client.ExecuteWithPredicate(getImagesLinksRequest, token, IsImageSrcGetRequestSuccessfull);

                // Use regex to search for src=""
                var imageLinks = new List<string>();
                foreach (Match match in regex.Matches(response.Content))
                {
                    // split removes size limits
                    imageLinks.Add(match.Groups[1].Value.Split("?w=")[0]);
                }
                // Remove duplicates
                return new HashSet<string>(imageLinks).ToList();
            }
            catch (Exception e)
            {
                throw new ImageCreatorException(e, response);
            }
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
            if (!string.IsNullOrEmpty(response.Content) && !response.Content.Contains("Pending"))
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

        public void UpdateBingCookies(string messageText)
        {
            authCookie = "_U=" + messageText;
            client.DefaultParameters.RemoveParameter("Cookie", ParameterType.HttpHeader);
            client.AddDefaultHeader("Cookie", authCookie);
        }
    }
}
