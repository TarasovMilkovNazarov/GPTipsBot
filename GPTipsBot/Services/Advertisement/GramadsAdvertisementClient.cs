using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace GPTipsBot.Services;

public interface IAdvertisementClient
{
    Task SendPostToChat(long chatId);
}

public class GramadsAdvertisementClient : IAdvertisementClient
{
    public async Task SendPostToChat(long chatId)
    {
        if (AppConfig.IsDevelopment)
            return;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", AppConfig.GramadsBearerToken);

        var sendPostDto = new { SendToChatId = chatId };
        var json = JsonConvert.SerializeObject(sendPostDto);
        var content = new StringContent(json, Encoding.UTF8, "application/json");


        try
        {
            var response = await client.PostAsync("https://api.gramads.net/ad/SendPost", content);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }
        }
        catch (Exception e)
        {
            // ignore
        }

    }
}