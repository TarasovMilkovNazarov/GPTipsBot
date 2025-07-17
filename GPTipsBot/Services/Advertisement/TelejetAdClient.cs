using System.Net.Sockets;
using System.Text;
using Telegram.Bot.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GPTipsBot.Services
{
    public class TelejetAdClient
    {
        private readonly string _apiKey = AppConfig.TelejetApiKey;
        private const string BapPrefix = "/__bap";
        private static readonly (string, int) Addr = ("api.production.bap.codd.io", 8080);
        private const int ApiVersion = 3;
        private readonly UdpClient _udpClient;

        /// <summary>
        /// If this method returns true then update should be processed by bot otherwise ignore it
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public async Task<bool> HandleUpdateAsync(Update update)
        {
            return !IsBapUpdate(update);
        }

        public async Task SendToBapAsync(Update update, string method)
        {
            try
            {
                var dto = new TelejetDto
                {
                    ApiKey = _apiKey,
                    Version = ApiVersion,
                    Update = update,
                    Method = method
                };

                var json = Serialize(dto);
                var data = Encoding.UTF8.GetBytes(json);
                await _udpClient.SendAsync(data, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send data to BAP API. Status Code: {ex}");
            }
        }

        private string Serialize(TelejetDto dto)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };
            var json = JsonConvert.SerializeObject(dto, settings);

            return json;
        }

        private bool IsBapUpdate(Update update)
        {
            var data = update.CallbackQuery?.Data;

            return data != null && data.StartsWith(BapPrefix);
        }

        public TelejetAdClient()
        {
            _udpClient = new UdpClient(Addr.Item1, Addr.Item2);
        }
    }

    class TelejetDto
    {
        public string ApiKey { get; set; }
        public string Method { get; set; }
        public int Version { get; set; }
        public Update Update { get; set; }
    }
}
