using dotenv.net;
using GPTipsBot;
using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBotTests.Services
{
    public class IntegrationTests
    {
        Update telegramUpdate;
        TelegramBotClient telegramBotClient;
        TelegramBotWorker telegramBotWorker;
        readonly IServiceProvider _services = HostBuilder.CreateHostBuilder(new string[] { }).Build().Services;

        public IntegrationTests()
        {
            DotEnv.Fluent().WithProbeForEnv(10).Load();
            var host = HostBuilder.CreateHostBuilder(Array.Empty<string>()).Build();
            telegramBotWorker = _services.GetService<TelegramBotWorker>() ?? throw new ArgumentNullException();

            telegramUpdate = new Update
            {
                Id = 878620224,
                Message = new Message
                {
                    MessageId = 3923,
                    From = new User
                    {
                        Id = 486363646,
                        IsBot = false,
                        FirstName = "Aleksandr",
                        LastName = "Tarasov",
                        Username = "alanextar",
                        LanguageCode = "ru"
                    },
                    Date = DateTime.UtcNow,
                    Chat = new Chat
                    {
                        Id = 486363646,
                        Type = Telegram.Bot.Types.Enums.ChatType.Private,
                        Username = "alanextar",
                        FirstName = "Aleksandr",
                        LastName = "Tarasov"
                    },
                    Text = "/start"
                }
            };

            telegramBotClient = new TelegramBotClient(AppConfig.TelegramToken);
        }
        
        [Test]
        public async Task SendTextMessage()
        {
            var mainHandler = _services.GetRequiredService<MainHandler>();
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);
            await mainHandler.HandleAsync(updateDecorator, cts.Token);
            updateDecorator.Message.Text = "What is the capital city of France?";
            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            Assert.IsNotEmpty(updateDecorator.Message.Text);
            Assert.True(updateDecorator.Reply.Text.Contains("Paris"));
        }

        [Test]
        public async Task SetBotUiLanguage()
        {
            var mainHandler = _services.GetRequiredService<MainHandler>();
            telegramUpdate.Message.Text = "/setLang";
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);

            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            updateDecorator.Message.Text = "Русский";
            await mainHandler.HandleAsync(updateDecorator, cts.Token);
            CultureInfo.CurrentUICulture = new CultureInfo("ru");

            Assert.AreEqual(BotResponse.LanguageWasSetSuccessfully, updateDecorator.Reply.Text);
        }
        
        [Test]
        public async Task SendGenerateImageRequest()
        {
            var mainHandler = _services.GetRequiredService<MainHandler>();
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);
            updateDecorator.Message.Text = "/image гора";

            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            Assert.True(updateDecorator.Reply.Text.Contains("http"));
        }

        [Test, Order(999)]
        public async Task ViolateBreakLimiting()
        {
            var cts = new CancellationTokenSource();
            var mainHandler = _services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);
            await mainHandler.HandleAsync(updateDecorator, cts.Token);
            updateDecorator.Message.Text = "test";

            for (int i = 0; i < 5; i++)
            {
                mainHandler.HandleAsync(updateDecorator, cts.Token);
            }

            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            Assert.IsNotEmpty(updateDecorator.Message.Text);
            Assert.IsNotEmpty(updateDecorator.Reply.Text);
            Assert.IsTrue(updateDecorator.Reply.Text.Contains("Слишком много запросов") || updateDecorator.Reply.Text.Contains("Too many requests"));
            await Task.Delay(TimeSpan.FromMinutes(1));
        }

        [Test]
        public async Task ResetContext()
        {
            var mainHandler = _services.GetRequiredService<MainHandler>();
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);
            await mainHandler.HandleAsync(updateDecorator, cts.Token);
            var initialContextId = updateDecorator.Message.ContextId;

            updateDecorator.Message.Text = "/reset_context";
            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            var newContextId = updateDecorator.Message.ContextId;

            Assert.False(initialContextId == newContextId);
        }
    }
}
