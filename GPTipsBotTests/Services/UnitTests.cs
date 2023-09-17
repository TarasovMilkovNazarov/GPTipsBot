using dotenv.net;
using GPTipsBot;
using GPTipsBot.Api;
using GPTipsBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Telegram.Bot;
using Telegram.Bot.Types;
using GPTipsBot.Extensions;

namespace GPTipsBotTests.Services
{
    public class UnitTests
    {
        private readonly Update telegramUpdate;
        private readonly TelegramBotClient botClient;
        private readonly ServiceProvider _services;

        public UnitTests()
        {
            _services = new ServiceCollection().ConfigureServices().AddTransient<IGpt, GptApiMock>().BuildServiceProvider();

            DotEnv.Fluent().WithProbeForEnv(10).Load();

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

            botClient = new TelegramBotClient(AppConfig.TelegramToken);
        }
        
        [Test]
        public async Task ViolateBreakLimiting()
        {
            var cts = new CancellationTokenSource();
            var mainHandler = _services.GetRequiredService<UpdateHandlerEntryPoint>();
            await mainHandler.HandleUpdateAsync(botClient, telegramUpdate, cts.Token);

            for (int i = 0; i < MessageService.MaxMessagesCountPerMinute; i++)
            {
                mainHandler = _services.GetRequiredService<UpdateHandlerEntryPoint>();
                await mainHandler.HandleUpdateAsync(botClient, telegramUpdate, cts.Token);
            }

            var updateDecorator = await mainHandler.HandleUpdateAsync(botClient, telegramUpdate, cts.Token);

            Assert.IsNotEmpty(updateDecorator.Message.Text);
            Assert.IsNotEmpty(updateDecorator.Reply.Text);
            Assert.IsTrue(updateDecorator.Reply.Text.Contains("Слишком много запросов") || updateDecorator.Reply.Text.Contains("Too many requests"));
        }
    }
}
