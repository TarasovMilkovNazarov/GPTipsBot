using GPTipsBot.Extensions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private string authCookie = "_IDET=MIExp=0; ipv6=hit=1684394748326&t=4; MUID=33BB5168EEE6652F2F8B4278EFAE64E7; MUIDB=33BB5168EEE6652F2F8B4278EFAE64E7; _EDGE_V=1; USRLOC=HS=1; SRCHD=AF=GENCRE; SRCHUID=V=2&GUID=E28E258438E04985B47E0EF40BFF663B&dmnchg=1; ANON=A=DB36387B8CD91D409173AF8DFFFFFFFF&E=1c54&W=1; NAP=V=1.9&E=1bfa&C=FvqKC6YnFSrDXs4BX_0VCeT0js1KjkambMl3Fg-uB7KgsQUzQwtAjw&W=1; PPLState=1; KievRPSSecAuth=FACSBBRaTOJILtFsMkpLVWSG6AN6C/svRwNmAAAEgAAACBdFpXtd+7piUASjCcvKGVYdugjmkWvcgxM2sEc0d8t5iyW/ocQHeT8wglJpNQk9UGmgpw1+njvGY3NfS5JYvxsGsATfYB4gljZ9xj3C1PlKL1mkLKA/Dx+2vvI1WUpNE+XfLPEO/PpGtkmTE6yUYUNQm20q9TmhpfP2TrD63u2QwUmjqI5lxRyymGgQvbC1o+Edg0/xeJYWbQt67/6C960r7lUhoC3i8ft8x7ty/RmscxOtiVL5qQ6LbiwfCNapo/V0xJbc/XWH5EN7DaINIr5gd/4EllNy4GufwAm4zFvRKL9ucXDubE1gnYWbgHijuFhiJtvvzM8XKHl8JIyr0Wz6FGvoCu33sG6IuZqxq1g/N14Xe8Y9sV90JbmyaNX83DJ6ifzKUiitHSoASPqUYE+emQzq67ik4sBbcQQVriwa8IaKG45y/4hwfTuPCEaAmmXWUCsa2sXhFui+T4pT/XVKdH1rilN3vgOGbT9f9r22y5bc6mqcqt7231lStz128If2dTSoJYlMzG2opcaQEiEuEBC81NqJegTaxXg4ezkGhtZUO5EV22hrGiboC0PKkZjd/dMBow3DUgrOMQqyUhZeHlkfZadKBdOvrbb9NDeVi+0BbmWVMzBRr2MaApv3qXICBeK7s9XF/XxK8aRegRqKuXBoFnMM92JvewOWeqlp3bXkfCdfj+4zhNxa0Un6HTrLvJNIiMDqKirNkQ7uKC5W4l7agiuwA4jNmP/Xo+w4P0eCBvCwpJ14rQGuVU8SNXSdYHv5YOtCBlufrQ9efsDoJucZWhFy/ql6bxSiKOxEVxXXU51RPht6Gf2n2Bv2PFriQtP2simFfx0bKme/Fcy28L0dU6M/FGGXx/IFnq+qmNyhh+/Zx5bZPZPDHe6yVBcQ1MmauDrhD4ozz9Bw2OaAM62ob+nV+N+SZR7x1RjA7mpYAetGtAK1V9GOIIVTXa4/FQhMazm87w0SKX6/QZ71bnz6EZq3x382OQyvKcrChDHa7zUnK4sZz7FJIIDvpe8FFGGPihsRoYgAp7FtLMjbVXWCuUr0PbzenUENaBe0oLouNK6gARpFYXlAds9eyJFocQLcFNib5NaSAbUiDrXQi0G1Tr2YFplxTN/T7HHlCh0CLmwdTo83j/kfwIvz82OpBiJ8AfMbpL+39kTwygngIktORzJTep0R2CXJBPc5RxvF4zrZcW7jqxZXL05pB7zK6KLSfrV+eQRyPNb1KalOHCN6pp0uShJYPPtRPfTWexHGVum82v/nDZ4ksfbZQxjuNkKynoVAqP8lLkDl1bsW1oNULu0t6Zk5D6VrZnGFvEQthn5U4Vl+5LSPZ4Xj2vJvVjts7akl20gJ76P/7Xt0s1XUXiTmoT4tjBGf16ZagxiFTXKAmETvdP7dg9EK6ptFV4ycAU6n45OnOYyoJh5011q9s3EZZlICVJm7GxeSUErCz+R/TG2z2ZaOoTO87lzlzkj2HbuZNbYUAItMS05Rtw5BaG037SymFci3uNAt; _U=18oaPaE5T-OPHgCoPXQZsGDAhnzaYaB_tS51nayL0YL-zxvK16Bm_ysi6_hEVvaqp6NO6ut2V4rvKrK9UMYngxlgbMWu2mm98U3VkECNs-1tBRtvpjIIHzcoMQbepsavPEFDE-N7jIZ6_udD8SajkQbQKj851Um_LjYzcfXq-0Z4F8Y7Y9DntENIIP7IQZrp2kEW1g644uZWexbvQUWDlDaOii6hjhj5szE6mwb_rYVdiej-AxMgRzFF-9YQwjA7gjT_PqIg9gT7r0OWrdqwQSw; WLID=q77FI4s+r9GeCp0LovNzuJx4KhOW4iVRPvbo+8ChBcnlwG3JoqrCKyaOnvGQSE9MOxAmfUqNocrk5YxZClg09KVhM9ucGiN5ChWfEBA/vUQ=; SRCHUSR=DOB=20230513&POEX=W&T=1683981286000; BCP=AD=1&AL=1&SM=1; MMCASM=ID=50869B7CFF8948F59F577E4A18D20298; BFB=AhCwtYMCPbNGaTjCqiOVRZS4O_SO-3q7vb3l6YbeC8M4aE2nOb1jl2j_eEtoltb1Rdyw2zzOewsW536ztwYpdQf0YqMRY0J64EiI1V7I0g3HXDsn6I_G6hUHQFvXgW7folhEGBXQO3bHxUuiZCvikrg0gOBM4UAXB5IWXwa0GwXcHCqYdXYxtOIPWx32lqdRfII; SRCHHPGUSR=SRCHLANG=en&PV=10.0.0&BRW=NOTP&BRH=S&CW=551&CH=572&SCW=1164&SCH=3675&DPR=1.0&UTC=300&DM=0&HV=1683981378&PRVCW=1042&PRVCH=969; SUID=A; _EDGE_S=SID=39D0239A13706F3739E8308F12B76E2F; WLS=C=2e52034796cc616c&N=%d0%90%d0%bb%d0%b5%d0%ba%d1%81%d0%b0%d0%bd%d0%b4%d1%80; GI_FRE_COOKIE=gi_prompt=1; _clck=c050nq|2|fbp|0|1228; _clck=c050nq|2|fbp|0|1228; _RwBf=mta=0&rc=30&rb=30&gb=0&rg=0&pc=30&mtu=0&rbb=0&g=0&cid=&clo=0&v=1&l=2023-05-17T07:00:00.0000000Z&lft=0001-01-01T00:00:00.0000000&aof=0&o=0&p=BINGCOPILOTWAITLIST&c=MR000T&t=6366&s=2023-05-12T14:03:35.1755316+00:00&ts=2023-05-18T06:25:46.4271431+00:00&rwred=0&wls=2&lka=0&lkt=0&TH=&e=q9x92paf1lZqUpNNub_dyynsL4cMMa4JTgDwyH9PS4kTu_WEZl5cxBdAo4EZzxPYyq_Kjr55Ad3I9whlqkf3Uw&A=; _SS=SID=39D0239A13706F3739E8308F12B76E2F&R=30&RB=30&GB=0&RG=0&RP=30; _clsk=bwd6qo|1684391146586|1|0|s.clarity.ms/collect; OID=AhAc33avKT3qh_iOG1ERTGjze3a1s_lw2Fm8IpRxHFl1fcw-qM2xsaPwNKQG3Q1V14lIXSYOhly9KYZhAdGhQYEI";
        private static HashSet<int> chromeDriverProcessIds = new HashSet<int>();
        
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
            var cookie = new System.Net.Cookie("_U", HttpUtility.UrlEncode(authCookie))
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

        private void RefreshAuthCookies()
        {
            foreach (var processId in chromeDriverProcessIds)
            {
                Process process = Process.GetProcessById(processId);
                process.Kill();
            }

            ChromeDriverService service = ChromeDriverService.CreateDefaultService();
            ChromeOptions options = new ChromeOptions();
            chromeDriverProcessIds.Add(service.ProcessId);

            options.AddArgument("--headless");
            IWebDriver driver = new ChromeDriver(service, options);

            driver.Navigate().GoToUrl("https://login.microsoftonline.com/");

            IWebElement emailInput = driver.FindElement(By.Id("i0116"));
            emailInput.SendKeys("your-email@example.com");

            IWebElement nextButton = driver.FindElement(By.Id("idSIButton9"));
            nextButton.Click();

            IWebElement passwordInput = driver.FindElement(By.Id("i0118"));
            passwordInput.SendKeys("your-password");

            IWebElement signInButton = driver.FindElement(By.Id("idSIButton9"));
            signInButton.Click();

            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            //wait.Until(ExpectedConditions.UrlContains("https://www.microsoft.com/"));

            driver.Navigate().GoToUrl("https://www.bing.com/");
            // Retrieve cookies
            var cookies = driver.Manage().Cookies.AllCookies;
            // Create a string representation of the cookies
            StringBuilder cookieString = new StringBuilder();
            foreach (var cookie in cookies)
            {
                cookieString.Append($"{cookie.Name}={cookie.Value}; ");
            }

            authCookie = cookieString.ToString();

            //driver.Quit();
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
