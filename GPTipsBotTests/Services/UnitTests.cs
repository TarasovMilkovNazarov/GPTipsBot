using dotenv.net;
using GPTipsBot;
using GPTipsBot.Services;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Telegram.Bot.Types;
using GPTipsBot.Extensions;
namespace GPTipsBotTests.Services
{
    public class UnitTests
    {
        private readonly Update telegramUpdate;
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
        }
        
        [Test]
        public async Task ViolateBreakLimiting()
        {
            var mainHandler = _services.GetRequiredService<UpdateHandlerEntryPoint>();
            await mainHandler.HandleUpdateAsync(telegramUpdate);

            for (var i = 0; i < RateLimitCache.MaxMessagesCountPerMinute; i++)
            {
                mainHandler = _services.GetRequiredService<UpdateHandlerEntryPoint>();
                await mainHandler.HandleUpdateAsync(telegramUpdate);
            }

            // var updateDecorator = await mainHandler.HandleUpdateAsync(telegramUpdate);
            //
            // Assert.IsNotEmpty(updateDecorator.Message.Text);
            // Assert.IsNotEmpty(updateDecorator.Reply.Text);
            // Assert.IsTrue(updateDecorator.Reply.Text.Contains("Слишком много запросов") || updateDecorator.Reply.Text.Contains("Too many requests"));
        }
    }
}
