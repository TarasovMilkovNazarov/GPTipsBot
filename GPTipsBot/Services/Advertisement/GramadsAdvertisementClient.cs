using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace GPTipsBot.Services;

public class GramadsAdvertisementClient
{
    public async Task SendPostToChat(long chatId)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", AppConfig.GramadsBearerToken);

        var sendPostDto = new { SendToChatId = chatId };
        var json = JsonConvert.SerializeObject(sendPostDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.gramads.net/ad/SendPost", content);

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        // var result = await response.Content.ReadAsStringAsync();
        // Console.WriteLine("Gramads: " + result);
    }
}