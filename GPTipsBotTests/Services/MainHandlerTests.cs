using dotenv.net;
using GPTipsBot.Db;
using GPTipsBot.Extensions;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using File = Telegram.Bot.Types.File;
using Range = Moq.Range;

namespace GPTipsBotTests.Services
{
    public class MainHandlerTests
    {
        private readonly Update startTelegramUpdate;
        private readonly IServiceCollection serviceCollection;
        private IServiceProvider services;
        private readonly Mock<ITelegramBotClient> botClientMock = new();
        private MessageRepository messageRepository;
        private readonly Mock<IGpt> gptMock;
        private readonly Mock<IImageGenerator> _imageGeneratorMock;

        private ITelegramBotClient BotClient => botClientMock.Object;

        public MainHandlerTests()
        {
            DotEnv.Fluent().WithProbeForEnv(10).Load();
            serviceCollection = new ServiceCollection().ConfigureServices();

            var recognitionServiceMock = new Mock<ITextRecognizer>();
            _imageGeneratorMock = new Mock<IImageGenerator>();
            recognitionServiceMock.Setup(s => s.Recognize(It.IsAny<string>())).ReturnsAsync(TestConstants.ImageTextResponse);
            _imageGeneratorMock.Setup(s => s.GenerateImage(It.IsAny<string>())).ReturnsAsync(TestConstants.GeneratedImage);
            gptMock = GptApiMock.CreateGptMock();

            serviceCollection
                .AddSingleton(recognitionServiceMock.Object)
                .AddSingleton(_imageGeneratorMock.Object)
                .AddSingleton(BotClient)
                .AddSingleton<IGpt>(gptMock.Object);

            botClientMock.Setup(b => b.MakeRequestAsync(It.IsAny<GetFileRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new File
            {
                FileId = "test",
                FileSize = 1,
                FilePath = "test.txt"
            });

            botClientMock.Setup(b => b.MakeRequestAsync(
                It.IsAny<SendMessageRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Message
            {
                MessageId = 123
            });

            startTelegramUpdate = CreateTelegramUpdate(1234, 1234, BotMenu.StartCommand);
        }

        private static Update CreateTelegramUpdate(int updateId, int messageId, string? text, long chatId = TestConstants.UserId)
        {
            return new Update
            {
                Id = updateId,
                Message = new Message
                {
                    MessageId = messageId,
                    From = new User
                    {
                        Id = chatId,
                        IsBot = false,
                        FirstName = "Aleksandr",
                        LastName = "Tarasov",
                        Username = "alanextar",
                        LanguageCode = "ru"
                    },
                    Date = DateTime.UtcNow,
                    Chat = new Chat
                    {
                        Id = chatId,
                        Type = Telegram.Bot.Types.Enums.ChatType.Private,
                        Username = "alanextar",
                        FirstName = "Aleksandr",
                        LastName = "Tarasov"
                    },
                    Text = text
                }
            };
        }

        [SetUp]
        public async Task Setup()
        {
            ResetRequestsRateLimit();
            services = serviceCollection.BuildServiceProvider();
            messageRepository = services.GetRequiredService<MessageRepository>();
            var appContext = services.GetRequiredService<ApplicationContext>();
            await ClearDatabase(appContext);
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // Set once for all tests in this class
            Environment.SetEnvironmentVariable("ConnectionString",
                "Server=localhost;Port=5434;Database=gptips;User Id=postgres;Password=postgres;");
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
        public async Task SendTextMessage_NewUser_ReturnsGtpResponse()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();

            var messageUpd = CreateTelegramUpdate(1, 2, "What is the capital city of France?");
            var messageUpdDecorator = new UpdateDecorator(messageUpd);
            await mainHandler.HandleAsync(messageUpdDecorator);

            messageUpdDecorator.Reply.Text.Should().Be("test");
        }

        [Test]
        public async Task SendTextMessage_ManyRequestsPerMinute_LimitExceeded()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();

            var messageUpd = CreateTelegramUpdate(1, 2, "What is the capital city of France?");

            UpdateDecorator messageUpdDecorator = null!;
            for (var i = 0; i < RateLimitCache.MaxMessagesCountPerMinute + 1; i++)
            {
                messageUpdDecorator = new UpdateDecorator(messageUpd);
                await mainHandler.HandleAsync(messageUpdDecorator);
            }

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == messageUpdDecorator.ChatId &&
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            gptMock.Verify(g => g.SendMessage(It.IsAny<UpdateDecorator>(),
                It.IsAny<CancellationToken>()), Times.Exactly(RateLimitCache.MaxMessagesCountPerMinute));
        }

        [Test]
        public async Task SetBotUiLanguageCommand_RussianCulture_ReturnsChooseLanguageInstruction()
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ru");
            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(CreateTelegramUpdate(1, 2, BotMenu.ChooseLangCommand));

            await mainHandler.HandleAsync(updateDecorator);

            updateDecorator.Reply.Text.Should().Be(BotResponse.ChooseLanguagePlease);
        }

        [Test]
        public async Task GenerateImageRequest_ManyRequests_ImagesPerDayLimitResponse()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var getImageUpdate = CreateTelegramUpdate(1, 2, "/image гора");
            UpdateDecorator updateDecorator = null!;

            for (int i = 0; i < ImageGeneratorHandler.ImagesPerDayLimit + 1; i++)
            {
                updateDecorator = new UpdateDecorator(getImageUpdate);
                await mainHandler.HandleAsync(updateDecorator);
            }

            var generatedImagesCount = messageRepository.GetTodayImagesCount(updateDecorator.UserChatKey);

            generatedImagesCount.Should().Be(ImageGeneratorHandler.ImagesPerDayLimit);

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == updateDecorator.ChatId &&
                    arg.Text == String.Format(BotResponse.ImagesPerDayLimit, ImageGeneratorHandler.ImagesPerDayLimit)
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _imageGeneratorMock.Verify(g => g.GenerateImage("гора"),
                Times.Exactly(ImageGeneratorHandler.ImagesPerDayLimit));
        }

        [Test]
        public async Task GenerateImageRequest_ImageWithDescriptionCommand_GenerateImage()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var getImageUpdate = CreateTelegramUpdate(1, 2, "/image гора");
            var updateDecorator = new UpdateDecorator(getImageUpdate);
            await mainHandler.HandleAsync(updateDecorator);

            var generatedImagesCount = messageRepository.GetTodayImagesCount(updateDecorator.UserChatKey);

            generatedImagesCount.Should().Be(1);
        }

        [Test]
        public async Task RecognizeImageTextRequest_ImageTextRecognizeCommand_ReturnsText()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var update = CreateTelegramUpdate(1, 2, BotMenu.ImageTextRecognizeCommand);
            var commandUpdate = new UpdateDecorator(update);
            await mainHandler.HandleAsync(commandUpdate);
            update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[] { new PhotoSize
                {
                    FileId = "test"
                }
            };

            var sendImageToProccessUpd = new UpdateDecorator(update);
            await mainHandler.HandleAsync(sendImageToProccessUpd);

            commandUpdate.Reply.Text.Should().Be(BotResponse.SendTextRecognitionImage);
            sendImageToProccessUpd.Reply?.Text.Should().Be(TestConstants.ImageTextResponse);
        }

        [Test]
        public async Task RecognizeImageTextRequest_ImageFirst_ChooseCommandFirstResponse()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[] { new PhotoSize
                {
                    FileId = "test"
                }
            };

            var sendImageToProccessUpd = new UpdateDecorator(update);
            await mainHandler.HandleAsync(sendImageToProccessUpd);

            var sendMessageRequestExpected = new SendMessageRequest(sendImageToProccessUpd.UserChatKey.Id,
                BotResponse.SendImageTextRecognitionCommandFirst)
            {
                ReplyMarkup = TelegramBotUiService.CancelKeyboard
            };

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == sendMessageRequestExpected.ChatId &&
                    arg.Text == BotResponse.SendImageTextRecognitionCommandFirst
                ),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ResetContext_OldContextExists_ReturnNewContextId()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var startUpdateDecorator = new UpdateDecorator(startTelegramUpdate);
            await mainHandler.HandleAsync(startUpdateDecorator);
            var initialContextId = startUpdateDecorator.Message.ContextId;

            var resetContextUpdDecorator = new UpdateDecorator(CreateTelegramUpdate(1, 2, BotMenu.ResetContextCommand));
            await mainHandler.HandleAsync(resetContextUpdDecorator);

            var newContextId = resetContextUpdDecorator.Message.ContextId;

            newContextId.Should().NotBe(initialContextId);
        }

        [Test]
        public async Task SendMessage_ContextExists_SameContext()
        {
            var mainHandler = services.GetRequiredService<MainHandler>();
            var startUpdateDecorator = new UpdateDecorator(startTelegramUpdate);
            await mainHandler.HandleAsync(startUpdateDecorator);
            var initialContextId = startUpdateDecorator.Message.ContextId;

            var firstMessageUpd = new UpdateDecorator(CreateTelegramUpdate(1,2, "first"));
            await mainHandler.HandleAsync(firstMessageUpd);

            var secondMessageUpd = new UpdateDecorator(CreateTelegramUpdate(3,4, "second"));
            await mainHandler.HandleAsync(secondMessageUpd);

            var newContextId = secondMessageUpd.Message.ContextId;

            newContextId.Should().Be(initialContextId);
        }

        [Test]
        public async Task StartCommand_UserNotExists_NewUserAdded()
        {
            var userRepository = services.GetRequiredService<UserRepository>();
            var context = services.GetRequiredService<ApplicationContext>();
            try
            {
                userRepository.Delete(TestConstants.UserId);
                await context.SaveChangesAsync();
            }
            catch (Exception)
            {
                // ignored
            }

            var mainHandler = services.GetRequiredService<MainHandler>();
            var updateDecorator = new UpdateDecorator(startTelegramUpdate);
            updateDecorator.Message.ContextBound = true;
            await mainHandler.HandleAsync(updateDecorator);

            var newUser = userRepository.Get(TestConstants.UserId);

            newUser.Should().NotBeNull();
        }

        private async Task ClearDatabase(ApplicationContext context)
        {
            var entityTypes = context.Model.GetEntityTypes().ToList();

            foreach (var entityType in entityTypes)
            {
                var tableName = entityType.GetTableName();
                await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE;");
            }

            await context.SaveChangesAsync();
        }
    }
}
