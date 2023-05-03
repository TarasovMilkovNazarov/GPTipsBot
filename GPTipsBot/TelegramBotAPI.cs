using RestSharp;

namespace GPTipsBot
{
    public class TelegramBotAPI
    {
        private string _botToken;
        private readonly long _chatId;
        private readonly string baseUrl;
        private readonly RestClient telegramHttpClient;

        public TelegramBotAPI(string botToken, long chatId)
        {
            _botToken = botToken;
            _chatId = chatId;
            baseUrl = $"https://api.telegram.org/bot{_botToken}";
            telegramHttpClient = new RestClient(baseUrl);
        }
        
        public void SetMyDescription(string messageText)
        {
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
        public string GetMyDescription()
        {
            var request = new RestRequest("getMyDescription", Method.Get);
            request.AddParameter("chat_id", _chatId);
            RestResponse response = null;

            try
            {
                response = telegramHttpClient.Execute(request);
            }
            catch (Exception ex)
            {
                return "";
            }

            return response.Content;
        }
    }
}
