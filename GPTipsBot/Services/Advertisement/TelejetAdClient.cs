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
        private static readonly string _bapPrefix = "/__bap";
        private static readonly (string, int) _addr = ("api.production.bap.codd.io", 8080);
        private static readonly int _apiVersion = 3;
        private readonly UdpClient _udpClient;

        public void SendAdvertisement(Update update)
        {
            if (IsBapUpdate(update))
                return;

            SendToBapAsync(update, "advertisement");
        }

        /// <summary>
        /// If this method returns true then update should be processed by bot otherwise ignore it
        /// </summary>
        /// <param name="update"></param>
        /// <returns></returns>
        public async Task<bool> HandleUpdateAsync(Update update)
        {
            await SendToBapAsync(update, "activity");

            return !IsBapUpdate(update);
        }

        private async Task SendToBapAsync(Update update, string method)
        {
            try
            {
                var dto = new TelejetDto
                {
                    ApiKey = _apiKey,
                    Version = _apiVersion,
                    Update = update,
                    Method = method
                };

                string json = Serialize(dto);
                byte[] data = Encoding.UTF8.GetBytes(json);
                var result = await _udpClient.SendAsync(data, data.Length);
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
            string json = JsonConvert.SerializeObject(dto, settings);

            return json;
        }

        public bool IsBapUpdate(Update update)
        {
            var data = update.CallbackQuery?.Data;

            return data != null && data.StartsWith(_bapPrefix);
        }

        public TelejetAdClient()
        {
            this._udpClient = new UdpClient(_addr.Item1, _addr.Item2);
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
