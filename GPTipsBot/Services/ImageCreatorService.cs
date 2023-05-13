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
        private string authCookie = "ipv6=hit=1683984301209&t=4; MUID=1BFAF826D5F164F5079CEB29D4D965D5; USRLOC=HS=1; SRCHD=AF=SHORUN; SRCHUID=V=2&GUID=C044A514B78149B594D67939760B330A&dmnchg=1; MUIDB=1BFAF826D5F164F5079CEB29D4D965D5; _UR=QS=0&TQS=0; ipv6=hit=1683903202726&t=4; MMCASM=ID=0533AEDB1D8E450A9EFC8F2875A63D50; BCP=AD=1&AL=1&SM=1; dsc=order=ShopOrderNewsOverShop; _HPVN=CS=eyJQbiI6eyJDbiI6MSwiU3QiOjAsIlFzIjowLCJQcm9kIjoiUCJ9LCJTYyI6eyJDbiI6MSwiU3QiOjAsIlFzIjowLCJQcm9kIjoiSCJ9LCJReiI6eyJDbiI6MSwiU3QiOjAsIlFzIjowLCJQcm9kIjoiVCJ9LCJBcCI6dHJ1ZSwiTXV0ZSI6dHJ1ZSwiTGFkIjoiMjAyMy0wNS0xMlQwMDowMDowMFoiLCJJb3RkIjowLCJHd2IiOjAsIkRmdCI6bnVsbCwiTXZzIjowLCJGbHQiOjAsIkltcCI6NH0=; MicrosoftApplicationsTelemetryDeviceId=552d2863-bb45-4cf3-a1fc-2111e63dcf76; _EDGE_S=SID=2EE1676F8E65688020A474608F5669EA; ANON=A=DB36387B8CD91D409173AF8DFFFFFFFF&E=1c53&W=1; NAP=V=1.9&E=1bf9&C=2RVPCtWCi_qRO4A5LEiCRVgfPtpar5WAN3Kz0myldEsU7SXZdUT6SA&W=1; PPLState=1; WLID=q77FI4s+r9GeCp0LovNzuJx4KhOW4iVRPvbo+8ChBcnlwG3JoqrCKyaOnvGQSE9MOxAmfUqNocrk5YxZClg09KVhM9ucGiN5ChWfEBA/vUQ=; CSRFCookie=2e29563b-2628-45d9-93a5-d565d383eb35; SRCHUSR=DOB=20230512&T=1683899606000&POEX=W; WLS=C=2e52034796cc616c&N=%d0%90%d0%bb%d0%b5%d0%ba%d1%81%d0%b0%d0%bd%d0%b4%d1%80; KievRPSSecAuth=FACSBBRaTOJILtFsMkpLVWSG6AN6C/svRwNmAAAEgAAACNANfL+sJDw+UAS58RDHUPL7uJmNvzDwH68NxnCyNP+39K2bDZtCYtsDNJAxGNDxG2dC/Qq0M8CDbhuhZ6ISYTGlR76W2ay3juBzKuyti77oM/UjUJ1wwouaYUmIP2RwqmmZrzwu3kjdOUbTcMSwGXABj06QyxtnNLjjOWnskHbeq5+OYHv9BaiWHpPCRmSio2C+esRuTYEz16ZAInplodHiXJXlxmL9r27BUmUt8UZm8Om1OskQNa3oxCr70Y8QzBzTyakNkpU0aozA4MT/iTuLvV1QPuIW5BOhLPiKm+lAkKC60KhSsA4MwLA+9Toj2V59JL7uSImd+OoRHp2x8iStjKBJuc5A+GjixzBhX5eppZvHMn0HWfs+okYE8Ud1dfVcMNf90JlM/he4p4UP7dnN4i6eJLoWvILgodKlmEjZ3JTzST5ZRag8XYxlJZc3VldTHHlWgBQcJ4AlX2Cj280L2b/xyXC297yId76KS0GaYu9aGMd7/eW2NuDSJVUDmQPGnBNDax5T5jhnVO40WOeHQ4FbYKu3QyT2QGZkLPNfiaAjQD2NQR6E7aVE+P+7PR4XSB9Ng480M4WX4Dr8rkxCDPM/qg3SkgCmV8RthO0U67qboM432yrcrEzO06DPaj5EJ7ipHhDJerD+R/eX/wR7PCnTjBPeThpfGjywD9zjOC/6trB1xI5Dck7k+pvR5pagtNV7ozHw0NOn7ekcBy6yH9wNX9I4+FtDLzybCObko9PQR+VEwiFSO9HxXUDser1eJ2bsqpYj6+SE+Y6IHB26YjoG1i/FgkC4e4KfWSye1askg7yOUpYFFGM6hqHLg1oCnHFqMufuRCmEqYXPF6bRQfzxWCkePhilMnhk7LsyP7OvhLNrbDSfi4pOFafEWhwWfRwNSXv3XtIzhT/DVWrNvkIOPsOUzm+x53XY83z+yY8g32XtS9jqmQPgcyVcwWPyWPe+jwcz05QPIjoHaXL1hc5cJApAJNZlwRp4oVkopV0dm05ibaUPQfZXjetuxHU0+/gIVHdjnmOAYpRVP/uK+JYZvUstiE9KZTE4Xbxnt01DhWUFVaUM4pNXGFS8cUQkWxhbK4WTSU3VH0yHfRHnBRk2eQ42rimr4ACti5EnGasYcMH69fczOGHHMzPSh9gNGtzaEywLe6LFyH3mosFpJrYQaDIr6Jm+KmoqKXxbS8bsKeiwOUVib+Sp5yoTmA4SESPsdqTWbxhs4RncTZCOK7IphkamCDP4+F45Zmy+oKKTD8DYAxSpSahtQRFlRsP3aufbwaOd13SG4nsk1lRfYHb5QYVZKSj0ury5+PNYjHHYy4DOIVUnQLPrlJM98wG1lqJnnqv5iZOnng7ToHY1L/Dif+oAeYs4foEkakA36sUvT3vX4lJdLTH1E1+AHVgGkWRiOVcjrE4zYed4alxTf7TQkAG/bQO63de3NxX9YTh/hRdzF40FCKkbIszgHDbx9lA4FhVTfsUUAJ10jlchjUbe5JK0RrGsLeDwTAKw; _U=1k2N7azlv_lhDR8Qv01Op4YtxQf8LsxNRDrCVRJ7SvdPknimyCnyN0c3xmfrNgBZX6HfjXbrmxGw5IxgcnmEhM2GlMWTPT7PRHgS7KmOG3Wea96KkXu-7kY3Av3ltPfSAkEwffOJjSOUaOeBJQEo1xaBNrWfSDZ21JAg3WBymJK_KrV7BSDfeXA7uhqaihx3b6v26MGulYn9R2E8XkJEuk4SnBYdDFLeplPUFzU92_iyFZYiRgA4R_O1bXU6nHZfOmLmQvwmA9NqA8HDgEFEwaQ; BFB=AhCkK-pUiGTjyCQSVPmpUH9yZQ--T-qEOWcNkvgvyLXdxQ6loOlfsI5kjrg6V6j27F-4J6ePr3GyMXuZt_lmopyROPnNObkJtmJFRDWVk4TvCjsGwohBJLSKKj5WEL17lB-M4coUJv7AIrnqFBcb7qGeZ93cAqrpeczf63CFRNVll1UvQw03ttwR8ljISOOKHZM; _clck=16vrmwf|2|fbk|0|1227; SUID=A; SRCHHPGUSR=SRCHLANG=en&PV=10.0.0&BRW=XW&BRH=M&CW=1865&CH=969&SCW=1850&SCH=1819&DPR=1.0&UTC=300&DM=0&WTS=63819496398&HV=1683899944&EXLTT=2&PRVCW=1865&PRVCH=969; GI_FRE_COOKIE=gi_prompt=5&gi_fre=2&gi_sc=6; _clsk=bu4ptg|1683980710623|5|1|v.clarity.ms/collect; OID=AhCo4H5puhAzsEaMMP666M5hfiIFlQC25TSR68w4sAiHgcOaZEkul9xNOAsuLCxJsy2wI4uf9RW2mWKNgGunc9eq; _SS=SID=0B78011807C4699B092B121706F7688C&R=9&RB=9&GB=0&RG=0&RP=6; _RwBf=ilt=8&ihpd=2&ispd=3&rc=9&rb=9&gb=0&rg=0&pc=6&mtu=0&rbb=0&g=0&cid=&clo=0&v=4&l=2023-05-13T07:00:00.0000000Z&lft=0001-01-01T00:00:00.0000000&aof=0&o=0&p=BINGCOPILOTWAITLIST&c=MR000T&t=6366&s=2023-05-12T14:03:35.1755316+00:00&ts=2023-05-13T12:25:11.1517748+00:00&rwred=0&wls=2&lka=0&lkt=0&TH=&mta=0&e=q9x92paf1lZqUpNNub_dyynsL4cMMa4JTgDwyH9PS4kTu_WEZl5cxBdAo4EZzxPYyq_Kjr55Ad3I9whlqkf3Uw&A=DB36387B8CD91D409173AF8DFFFFFFFF";

        public ImageCreatorService() {
            client = CreateBingRestClient();
            regex = new Regex(@"src=""([^""]+)""");
        }

        public void CreateImageFromText(string prompt)
        {
            var imgSrcs = GetImageSources(prompt);
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

            //client.AddDefaultHeader("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            //client.AddDefaultHeader("accept-language", "en-US,en;q=0.9");
            //client.AddDefaultHeader("cache-control", "max-age=0");
            //client.AddDefaultHeader("content-type", "application/x-www-form-urlencoded");
            //client.AddDefaultHeader("referrer", "https://www.bing.com/images/create/");
            //client.AddDefaultHeader("origin", BING_URL);
            //client.AddDefaultHeader("Cookie", authCookie);

            return client;
        }

        private List<string> GetImageSources(string prompt)
        {
            Console.WriteLine("Sending request...");
            string urlEncodedPrompt = Uri.EscapeDataString(prompt);
            // https://www.bing.com/images/create?q=<PROMPT>&rt=4&FORM=GENCRE
            string url = $"images/create?q={urlEncodedPrompt}&rt=4&FORM=GENCRE";
            var request = new RestRequest(url, Method.Post);

            request.AddHeader("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.AddHeader("accept-language", "en-US,en;q=0.9");
            request.AddHeader("cache-control", "max-age=0");
            request.AddHeader("content-type", "application/x-www-form-urlencoded");
            request.AddHeader("referrer", "https://www.bing.com/images/create/");
            request.AddHeader("origin", BING_URL);
            request.AddHeader("Cookie", authCookie);

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
            Console.WriteLine("Waiting for results...");
            while (true)
            {
                Console.Write(".");
                response = client.Execute(new RestRequest(pollingUrl, Method.Get));
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Could not get results");
                }
                if (response.Content == "")
                {
                    Thread.Sleep(1000);
                    continue;
                }
                else
                {
                    break;
                }
            }

            // Use regex to search for src=""
            var imageLinks = new List<string>();
            foreach (Match match in regex.Matches(response.Content))
            {
                imageLinks.Add(match.Groups[1].Value);
            }
            // Remove duplicates
            return new List<string>(new HashSet<string>(imageLinks));
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
    }
}
