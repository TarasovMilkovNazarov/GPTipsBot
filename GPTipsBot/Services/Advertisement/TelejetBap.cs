using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Telegram.Bot.Types;
using Newtonsoft.Json;

namespace GPTipsBot.Services
{
    public class Bap
    {
        private readonly string _apiKey = AppConfig.TelejetApiKey;
        private static readonly string _bapPrefix = "/__bap";
        private static readonly (string, int) _addr = ("api.production.bap.codd.io", 8080);
        private static readonly int _apiVersion = 3;

        public void SendAdvertisement(Update update)
        {
            if (IsBapUpdate(update))
                return;

            SendToBap(update, "advertisement");
        }

        public void HandleUpdate(Update update)
        {
            SendToBap(update, "activity");
        }

        private void SendToBap(Update update, string method)
        {
            using (UdpClient udpClient = new UdpClient())
            {
                try
                {
                    var obj = new
                    {
                        api_key = _apiKey,
                        version = _apiVersion,
                        update
                    };
                    string json = JsonConvert.SerializeObject(obj);

                    byte[] data = Encoding.UTF8.GetBytes(json);
                    udpClient.Send(data, data.Length, _addr.Item1, _addr.Item2);
                    udpClient.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send data to BAP API. Status Code: {ex}");
                }
            }
        }

        public bool IsBapUpdate(Update update)
        {
            var data = update.CallbackQuery?.Data;

            return data != null && data.StartsWith(_bapPrefix);
        }

        public Bap()
        {
        }
    }
}
