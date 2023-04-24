using RestSharp;

namespace GPTipsBot
{
    public class TelegramBotAPI
    {
        private string _botToken;
        private readonly long _chatId;
        private readonly string baseUrl;

        public TelegramBotAPI(string botToken, long chatId)
        {
            _botToken = botToken;
            _chatId = chatId;
            baseUrl = $"https://api.telegram.org/bot{_botToken}";
        }

        public void SetMyDescription(string messageText)
        {
            var telegramHttpClient = new RestClient(baseUrl);
            var request = new RestRequest("setMyDescription", Method.Post);
            var botDescription = "Description";
            request.AddParameter("chat_id", _chatId);
            request.AddParameter("text", botDescription);
            RestResponse response = null;

            try
            {
                response = telegramHttpClient.Execute(request);
            }
            catch (Exception ex)
            {
                // Handle exception
            }

            if (response != null && response.IsSuccessful)
            {
                Console.WriteLine("Description was set successfully!");
            }
            else
            {
                Console.WriteLine("Error sending message: " + response.StatusDescription);
            }
        }
    }
}
