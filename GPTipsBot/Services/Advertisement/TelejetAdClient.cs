using System.Net.Sockets;
using System.Text;
using GPTipsBot.Config;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Telegram.Bot.Types;

namespace GPTipsBot.Services
{
    public class TelejetAdClient(ILogger<TelejetAdClient> logger): IDisposable
    {
        private readonly string _apiKey = AppConfig.TelejetApiKey;
        private const string BapPrefix = "/__bap";
        private const int ApiVersion = 3;
        private readonly UdpClient _udpClient = new("bap.teleads.pro", 8080);

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
                logger.LogError(ex, "Failed to send data to BAP API");
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

        public void Dispose()
        {
            _udpClient.Dispose();
        }
    }

    class TelejetDto
    {
        [JsonProperty("api_key")]
        public string ApiKey { get; set; }
        [JsonProperty("method")]
        public string Method { get; set; }
        [JsonProperty("version")]
        public int Version { get; set; }
        [JsonProperty("update")]
        public Update Update { get; set; }
    }
}
