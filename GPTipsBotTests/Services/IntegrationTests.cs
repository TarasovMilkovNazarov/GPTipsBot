using dotenv.net;
using GPTipsBot;
using GPTipsBot.Db;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
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
        private readonly Update telegramUpdate;
        private readonly IServiceCollection serviceCollection;
        private IServiceProvider services;
        private ITelegramBotClient botClient;

        public IntegrationTests()
        {
            DotEnv.Fluent().WithProbeForEnv(10).Load();
            serviceCollection = new ServiceCollection().ConfigureServices();

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
            ResetRequestsRateLimit();

            services = serviceCollection.ConfigureServices().BuildServiceProvider();
            botClient = services.GetRequiredService<ITelegramBotClient>();
        }

        private void ResetRequestsRateLimit()
        {
            var descriptor = serviceCollection.FirstOrDefault(d => d.ServiceType == typeof(RateLimitCache));
            if (descriptor != null)
            {
                serviceCollection.Remove(descriptor);
            }

            serviceCollection.AddSingleton<RateLimitCache>();
        }

        [Test]
        public async Task SendTextMessage()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(telegramUpdate);
            await mainHandler.HandleAsync(updateDecorator);

            updateDecorator.Message.Text = "What is the capital city of France?";
            var message = await botClient.SendTextMessageAsync(updateDecorator.UserChatKey.ChatId, updateDecorator.Message.Text);
            updateDecorator.Message.TelegramMessageId = message.MessageId;
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator);

            Assert.IsNotEmpty(updateDecorator.Message.Text);
            Assert.True(updateDecorator.Reply.Text.Contains("Paris"));
        }

        [Test]
        public async Task SetBotUiLanguage()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(telegramUpdate);
            updateDecorator.Message.Text = BotMenu.ChooseLangCommand;

            await mainHandler.HandleAsync(updateDecorator);

            updateDecorator.Message.Text = "Русский";
            await mainHandler.HandleAsync(updateDecorator);
            CultureInfo.CurrentUICulture = new CultureInfo("ru");

            Assert.AreEqual(BotResponse.LanguageWasSetSuccessfully, updateDecorator.Reply.Text);
        }

        [Test]
        public async Task SendGenerateImageRequest()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(telegramUpdate);
            updateDecorator.Message.Text = "/image гора";

            await mainHandler.HandleAsync(updateDecorator);

            Assert.True(updateDecorator.Reply.Text.Contains("http"));
        }

        [Test]
        public async Task ResetContext()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(telegramUpdate);
            await mainHandler.HandleAsync(updateDecorator);
            var initialContextId = updateDecorator.Message.ContextId;

            updateDecorator.Message.Text = "/reset_context";
            var message = await botClient.SendTextMessageAsync(updateDecorator.UserChatKey.ChatId, updateDecorator.Message.Text);
            updateDecorator.Message.TelegramMessageId = message.MessageId;
            await mainHandler.HandleAsync(updateDecorator);

            var newContextId = updateDecorator.Message.ContextId;

            Assert.False(initialContextId == newContextId);
        }

        [Test]
        public async Task TestContext()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(telegramUpdate);
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator);
            var initialContextId = updateDecorator.Message.ContextId;

            
            updateDecorator.Message.Text = "2+2=?";
            var message = await botClient.SendTextMessageAsync(updateDecorator.UserChatKey.ChatId, updateDecorator.Message.Text);
            updateDecorator.Message.TelegramMessageId = message.MessageId;
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator);

            updateDecorator.Message.Text = "add 2 to result";
            message = await botClient.SendTextMessageAsync(updateDecorator.UserChatKey.ChatId, updateDecorator.Message.Text);
            updateDecorator.Message.TelegramMessageId = message.MessageId;
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator);

            var newContextId = updateDecorator.Message.ContextId;

            Assert.True(initialContextId == newContextId);
            Assert.True(updateDecorator.Reply.Text.Contains("6"));
        }

        [Test]
        public async Task AddNewUser()
        {
            var userRepository = services.GetRequiredService<UserRepository>();
            var context = services.GetRequiredService<ApplicationContext>();
            try
            {
                userRepository.Delete(AppConfig.AdminIds.First());
                await context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // ignored
            }

            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(telegramUpdate);
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator);

            var newUser = userRepository.Get(AppConfig.AdminIds.First());

            Assert.True(newUser != null);
        }
    }
}
