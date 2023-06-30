using GPTipsBot;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Newtonsoft.Json;
using NUnit.Framework;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBotTests.Services
{
    public class HandlerTests
    {
        Update telegramUpdate;
        TelegramBotClient telegramBotClient;
        TelegramBotWorker telegramBotWorker;
        ServiceProvider serviceProvider;

        public HandlerTests()
        {
            var services = new ServiceCollection();
            services.AddTransient<TelegramBotWorker>();

            serviceProvider = services.BuildServiceProvider();
            telegramBotWorker = serviceProvider.GetService<TelegramBotWorker>() ?? throw new ArgumentNullException();

            var updStr = "{\"update_id\":878620224,\"message\":" +
                "{\"message_id\":3923,\"from\":{\"id\":486363646,\"is_bot\":false,\"first_name\":\"Aleksandr\"," +
                "\"last_name\":\"Tarasov\",\"username\":\"alanextar\",\"language_code\":\"ru\"},\"date\":1688133454," +
                "\"chat\":{\"id\":486363646,\"type\":\"private\",\"username\":\"alanextar\",\"first_name\":\"Aleksandr\"," +
                "\"last_name\":\"Tarasov\"},\"text\":\"test\"}}";

            telegramUpdate = JsonConvert.DeserializeObject<Update>(updStr) ?? throw new ArgumentNullException();
            telegramBotClient = new TelegramBotClient(AppConfig.TelegramToken);
        }

        [Test]
        public async Task HandlersBaseOrderTest()
        {
            var mainHandler = serviceProvider.GetRequiredService<MainHandler>();
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);

            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            Assert.IsNotEmpty(updateDecorator.Message.Text);
        }

        [Test]
        public void SendTextMessage()
        {
            throw new NotImplementedException(); 
        }
        
        [Test]
        public void SendGenerateImageRequest()
        {
            throw new NotImplementedException();
        }

        [Test]
        public void ViolateBreakLimiting()
        {
            throw new NotImplementedException();
        }
    }
}
