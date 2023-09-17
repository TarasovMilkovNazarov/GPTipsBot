using dotenv.net;
using GPTipsBot;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBotTests.Services
{
    public class IntegrationTests
    {
        private readonly Update telegramUpdate;
        readonly IServiceProvider _services;
        private readonly ITelegramBotClient botClient;

        public IntegrationTests()
        {
            DotEnv.Fluent().WithProbeForEnv(10).Load();
            _services = new ServiceCollection().ConfigureServices().BuildServiceProvider();
            botClient = _services.GetRequiredService<ITelegramBotClient>();

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
        }

        [SetUp]
        public void Setup()
        {
            MessageService.ResetMessageCountsPerMinute(null);
        }

        [Test]
        public async Task SendTextMessage()
        {
            var mainHandler = _services.GetRequiredService<MainHandler>();
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);
            await mainHandler.HandleAsync(updateDecorator, cts.Token);
            updateDecorator.Message.Text = "What is the capital city of France?";
            updateDecorator.Message.ContextBound = true;
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

        [Test]
        public async Task TestContext()
        {
            var mainHandler = _services.GetRequiredService<MainHandler>();
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator, cts.Token);
            var initialContextId = updateDecorator.Message.ContextId;

            updateDecorator.Message.Text = "2+2=?";
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            updateDecorator.Message.Text = "add 2 to result";
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator, cts.Token);

            var newContextId = updateDecorator.Message.ContextId;

            Assert.True(initialContextId == newContextId);
            Assert.True(updateDecorator.Reply.Text.Contains("6"));
        }

        [Test]
        public async Task AddNewUser()
        {
            var userRepository = _services.GetRequiredService<UserRepository>();
            userRepository.Delete(AppConfig.AdminId);

            var mainHandler = _services.GetRequiredService<MainHandler>();
            var cts = new CancellationTokenSource();
            var updateDecorator = new UpdateDecorator(telegramUpdate, cts.Token);
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator, cts.Token);


            var newUser = userRepository.Get(AppConfig.AdminId);

            Assert.True(newUser != null);
        }
    }
}
