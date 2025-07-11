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
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoFixture;
using FluentAssertions;
using GPTipsBot;
using GPTipsBot.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OpenAI.ObjectModels.ResponseModels;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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
        private UpdateHandlerEntryPoint _updateHandlerEntryPoint;
        private readonly Mock<ITextRecognizer> _recognitionServiceMock;
        private readonly Fixture _fixture;
        private IMemoryCache _memoryCache;
        private UserCommandRepository userCommandRepository;

        private ITelegramBotClient BotClient => botClientMock.Object;

        public MainHandlerTests()
        {
            _fixture = new Fixture();
            DotEnv.Fluent().WithProbeForEnv(10).Load();
            serviceCollection = new ServiceCollection().ConfigureServices();

            _recognitionServiceMock = new Mock<ITextRecognizer>();
            _imageGeneratorMock = new Mock<IImageGenerator>();
            _recognitionServiceMock.Setup(s => s.Recognize(It.IsAny<string>())).ReturnsAsync(TestConstants.ImageTextResponse);
            _imageGeneratorMock.Setup(s => s.GenerateImage(It.IsAny<string>())).ReturnsAsync(TestConstants.GeneratedImage);
            gptMock = GptApiMock.CreateGptMock();

            serviceCollection
                .AddSingleton(_recognitionServiceMock.Object)
                .AddSingleton(_imageGeneratorMock.Object)
                .AddSingleton(BotClient)
                .AddSingleton<IGpt>(gptMock.Object)
                .AddSingleton(new Mock<GramadsAdvertisementClient>().Object);

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
            services = serviceCollection.BuildServiceProvider();
            ResetRequestsRateLimit();
            var appContext = services.GetRequiredService<ApplicationContext>();
            await ClearDatabase(appContext);
            messageRepository = services.GetRequiredService<MessageRepository>();
            userCommandRepository = services.GetRequiredService<UserCommandRepository>();
            _updateHandlerEntryPoint = services.GetRequiredService<UpdateHandlerEntryPoint>();
            _memoryCache = services.GetRequiredService<IMemoryCache>();
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
        public async Task UpdateHandler_BotMenuNavigation_UserCommandsSaved()
        {
            var commandSet = new List<CustomBotCommand>()
            {
                BotMenu.Start,
                BotMenu.ChooseLang,
                BotMenu.SetRuLang,
                BotMenu.Cancel
            };

            foreach (var command in commandSet)
            {
                var update = CreateTelegramUpdate(1, 2, command.Command);
                await _updateHandlerEntryPoint.HandleUpdateAsync(update);
            }

            var commands = userCommandRepository.Get(c => true).ToList();

            await userCommandRepository.GetLastAsync(1234);

            commands.Should().NotBeNull();
            commands.Count.Should().Be(commandSet.Count);
            commands.Select(c => c.Type).Should().BeEquivalentTo(commandSet.Select(c => c.Type));;
        }


        [Test]
        public async Task UpdateHandler_Kicked_Ignored()
        {
            var update = new Update()
            {
                Id = 1234,
                MyChatMember = new ChatMemberUpdated
                {
                    Chat = new Chat()
                    {
                        Id = 1234,
                    },
                    From = null,
                    Date = default,
                    OldChatMember = new ChatMemberMember(),
                    NewChatMember = new ChatMemberBanned(),
                    InviteLink = null,
                    ViaChatFolderInviteLink = null
                }
            };

            var updateHandlerFunc = async () => await _updateHandlerEntryPoint.HandleUpdateAsync(update);
            await updateHandlerFunc.Should().NotThrowAsync("Kicked member just ignored");
        }

        [Test]
        [TestCase(MessageType.Sticker)]
        [TestCase(MessageType.ChannelCreated)]
        [TestCase(MessageType.ChatTitleChanged)]
        public async Task UpdateHandler_MessageTypeArgument_Ignored(MessageType messageType)
        {
            var update = new Update()
            {
                Id = 1234,
                Message = new()
                {
                    Sticker = new Sticker()
                }
            };

            var updateHandlerFunc = async () => await _updateHandlerEntryPoint.HandleUpdateAsync(update);
            await updateHandlerFunc.Should().NotThrowAsync("Sticker message ignored");
        }

        [Test]
        public async Task SendTextMessage_NewUser_ReturnsGtpResponse()
        {
            var prompt = "What is the capital city of France?";
            var gtpResponse = "Paris";
            var response = new ChatCompletionCreateResponse
            {
                Choices = new() { new(){ Message = new("system", gtpResponse) } }
            };

            gptMock.Setup(m => m.SendMessage(It.Is<UpdateDecorator>(arg =>
                    arg.Message.Text.Equals(prompt)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var messageUpd = CreateTelegramUpdate(1, 2, prompt);
            var userId = messageUpd.Message.From.Id;
            await _updateHandlerEntryPoint.HandleUpdateAsync(messageUpd);

            gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg =>
                        arg.Message.Text.Equals(prompt)
                    ),
                It.IsAny<CancellationToken>()), Times.Once);

            var message = messageRepository.GetAllUserMessages(userId)
                .OrderByDescending(m => m.CreatedAt)
                .First();

            message.Should().NotBeNull();
            message.Text.Should().Be(gtpResponse);
        }

        [Test]
        public async Task SendTextMessage_ManyRequestsPerMinute_LimitExceeded()
        {
            var prompt = "How much it would be add 2 to previous result";
            var messageUpd = CreateTelegramUpdate(1, 2, prompt);

            for (var i = 0; i < RateLimitCache.MaxMessagesCountPerMinute + 1; i++)
            {
                await _updateHandlerEntryPoint.HandleUpdateAsync(messageUpd);
            }

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == messageUpd.Message.Chat.Id &&
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Exactly(2));

            gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                It.IsAny<CancellationToken>()), Times.Exactly(RateLimitCache.MaxMessagesCountPerMinute));
        }

        [Test]
        public async Task SendTextMessage_ManyParallelRequestsPerMinute_LimitExceeded()
        {
            var prompt = "SendTextMessage_ManyParallelRequestsPerMinute_LimitExceeded";
            var messageUpd = CreateTelegramUpdate(1, 2, prompt);

            var response = new ChatCompletionCreateResponse
            {
                Choices = new() { new() { Message = new("system", "test") } }
            };
            gptMock.Setup(x => x.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                    It.IsAny<CancellationToken>()))
                .Returns(async (UpdateDecorator upd, CancellationToken token) => {
                    // await Task.Delay(100, token);
                    return response;
                });

            for (var i = 0; i < RateLimitCache.MaxMessagesCountPerMinute + 1; i++)
            {
                await _updateHandlerEntryPoint.HandleUpdateAsync(messageUpd);
            }

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == messageUpd.Message!.Chat.Id &&
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                It.IsAny<CancellationToken>()), Times.Exactly(RateLimitCache.MaxMessagesCountPerMinute));
        }

        [Test]
        public async Task SetBotUiLanguageCommand_RussianCulture_ReturnsChooseLanguageInstruction()
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ru");
            var update = CreateTelegramUpdate(1, 2, BotMenu.ChooseLangCommand);

            await _updateHandlerEntryPoint.HandleUpdateAsync(update);

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == update.Message!.Chat.Id &&
                    arg.Text == BotResponse.ChooseLanguagePlease
                ),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task GenerateImageRequest_ManyRequests_ImagesPerDayLimitResponse()
        {
            var update = CreateTelegramUpdate(1, 2, "/image гора");

            for (int i = 0; i < ImageGeneratorHandler.ImagesPerDayLimit + 1; i++)
            {
                await _updateHandlerEntryPoint.HandleUpdateAsync(update);
            }

            var userId = update.Message!.From.Id;

            var generatedImagesCount = messageRepository.GetTodayImagesCount(userId);

            generatedImagesCount.Should().Be(ImageGeneratorHandler.ImagesPerDayLimit);

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == userId &&
                    arg.Text == String.Format(BotResponse.ImagesPerDayLimit, ImageGeneratorHandler.ImagesPerDayLimit)
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _imageGeneratorMock.Verify(g => g.GenerateImage("гора"),
                Times.Exactly(ImageGeneratorHandler.ImagesPerDayLimit));
        }

        [Test]
        public async Task GenerateImageRequest_ImageWithDescriptionCommand_GenerateImage()
        {
            var imagePromptWithCommand = "/image кракозябра";
            var getImageUpdate = CreateTelegramUpdate(1, 2, imagePromptWithCommand);
            await _updateHandlerEntryPoint.HandleUpdateAsync(getImageUpdate);

            var generatedImagesCount = messageRepository.GetTodayImagesCount(getImageUpdate.Message!.From.Id);

            generatedImagesCount.Should().Be(1);
        }

        [Test]
        public async Task RecognizeImageTextRequest_ImageTextRecognizeCommand_ReturnsText()
        {
            var update = CreateTelegramUpdate(1, 2, BotMenu.ImageTextRecognizeCommand);
            var userId = update.Message!.From.Id;

            await _updateHandlerEntryPoint.HandleUpdateAsync(update);
            update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[] { new PhotoSize
                {
                    FileId = "test"
                }
            };

            await _updateHandlerEntryPoint.HandleUpdateAsync(update);

            botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == userId &&
                    arg.Text == BotResponse.SendTextRecognitionImage
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _recognitionServiceMock.Verify(g => g.Recognize(It.IsAny<string>()),
                Times.Once);
        }

        [Test]
        public async Task RecognizeImageTextRequest_ImageFirst_ChooseCommandFirstResponse()
        {
            var update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[] { new PhotoSize
                {
                    FileId = "test"
                }
            };
            var userId = update.Message!.From.Id;

            await _updateHandlerEntryPoint.HandleUpdateAsync(update);

            var sendMessageRequestExpected = new SendMessageRequest(userId,
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
            await _updateHandlerEntryPoint.HandleUpdateAsync(startTelegramUpdate);
            var userId = startTelegramUpdate.Message!.From.Id;
            var initialContextId = messageRepository.GetLastContext(userId, userId);

            var resetContextUpdDecorator = CreateTelegramUpdate(1, 2, BotMenu.ResetContextCommand);
            await _updateHandlerEntryPoint.HandleUpdateAsync(resetContextUpdDecorator);

            var newContextId = messageRepository.GetLastContext(userId, userId);

            newContextId.Should().NotBe(initialContextId);
        }

        [Test]
        public async Task SendMessage_ContextExists_SameContext()
        {
            await _updateHandlerEntryPoint.HandleUpdateAsync(startTelegramUpdate);
            var userId = startTelegramUpdate.Message!.From.Id;
            var initialContextId = messageRepository.GetLastContext(userId, userId);

            var firstMessageUpd = CreateTelegramUpdate(1,2, "first");
            await _updateHandlerEntryPoint.HandleUpdateAsync(firstMessageUpd);

            var secondMessageUpd = CreateTelegramUpdate(3,4, "second");
            await _updateHandlerEntryPoint.HandleUpdateAsync(secondMessageUpd);

            var newContextId = messageRepository.GetLastContext(userId, userId);

            newContextId.Should().Be(initialContextId);
        }

        [Test]
        public async Task StartCommand_UserNotExists_NewUserAdded()
        {
            var userRepository = services.GetRequiredService<UserRepository>();

            await _updateHandlerEntryPoint.HandleUpdateAsync(startTelegramUpdate);

            var newUser = userRepository.Get(TestConstants.UserId);

            newUser.Should().NotBeNull();
        }

        [Test]
        public async Task TextMessage_TwoTimes_UserCached()
        {
            var userRepository = services.GetRequiredService<UserRepository>();

            await _updateHandlerEntryPoint.HandleUpdateAsync(startTelegramUpdate);
            await _updateHandlerEntryPoint.HandleUpdateAsync(startTelegramUpdate);

            var newUser = userRepository.Get(TestConstants.UserId);
            var cached = _memoryCache.Get<GPTipsBot.Models.User>("User_" + startTelegramUpdate.Message.From.Id);
            cached.Should().BeEquivalentTo(newUser);
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
