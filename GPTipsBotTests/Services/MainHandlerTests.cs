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
using AutoFixture;
using FluentAssertions;
using GPTipsBot;
using GPTipsBot.Dtos;
using GPTipsBot.Services.YandexCloud;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OpenAI.Chat;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using File = Telegram.Bot.Types.File;
using Message = Telegram.Bot.Types.Message;

namespace GPTipsBotTests.Services
{
    public partial class MainHandlerTests
    {
        private readonly Update _startTelegramUpdate;
        private readonly IServiceCollection _serviceCollection;
        private IServiceProvider _services;
        private readonly Mock<ITelegramBotClient> _botClientMock = new();
        private MessageRepository _messageRepository;
        private readonly Mock<IGpt> _gptMock;
        private readonly Mock<IImageGenerator> _imageGeneratorMock;
        private UpdateFirewall _updateFirewall;
        private readonly Mock<ITextRecognizer> _recognitionServiceMock;
        private readonly Fixture _fixture;
        private IMemoryCache _memoryCache;
        private UserCommandRepository _userCommandRepository;

        private ITelegramBotClient BotClient => _botClientMock.Object;

        public MainHandlerTests()
        {
            _fixture = new Fixture();
            DotEnv.Fluent().WithProbeForEnv(10).Load();
            _serviceCollection = new ServiceCollection().ConfigureServices();

            _recognitionServiceMock = new Mock<ITextRecognizer>();
            _imageGeneratorMock = new Mock<IImageGenerator>();
            _recognitionServiceMock.Setup(s => s.Recognize(It.IsAny<string>()))
                .ReturnsAsync(TestConstants.ImageTextResponse);
            _imageGeneratorMock.Setup(s => s.GenerateImage(It.IsAny<string>()))
                .ReturnsAsync(TestConstants.GeneratedImage);
            _gptMock = GptApiMock.CreateGptMock();
            var gramadsMockClient = new Mock<IAdvertisementClient>();

            _serviceCollection
                .AddSingleton(_recognitionServiceMock.Object)
                .AddSingleton(_imageGeneratorMock.Object)
                .AddSingleton(BotClient)
                .AddSingleton<IGpt>(_gptMock.Object)
                .AddSingleton(gramadsMockClient.Object);

            _botClientMock.Setup(b => b.MakeRequestAsync(It.IsAny<GetFileRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new File
            {
                FileId = "test",
                FileSize = 1,
                FilePath = "test.txt"
            });

            _botClientMock.Setup(b => b.MakeRequestAsync(
                It.IsAny<SendMessageRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Message
            {
                MessageId = 123
            });

            _startTelegramUpdate = CreateTelegramUpdate(1234, 1234, BotMenu.StartCommand);
        }

        private static Update CreateTelegramUpdate(
            int updateId,
            int messageId,
            string? text,
            long chatId = TestConstants.UserId)
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
            _services = _serviceCollection.BuildServiceProvider();
            ResetRequestsRateLimit();
            var appContext = _services.GetRequiredService<ApplicationContext>();
            await ClearDatabase(appContext);
            _messageRepository = _services.GetRequiredService<MessageRepository>();
            _userCommandRepository = _services.GetRequiredService<UserCommandRepository>();
            _updateFirewall = _services.GetRequiredService<UpdateFirewall>();
            _memoryCache = _services.GetRequiredService<IMemoryCache>();
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
            var descriptor = _serviceCollection.FirstOrDefault(d => d.ServiceType == typeof(RateLimiter));
            if (descriptor != null)
            {
                _serviceCollection.Remove(descriptor);
            }

            _serviceCollection.AddSingleton<RateLimiter>();
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
                await _updateFirewall.HandleUpdateAsync(update);
            }

            var commands = _userCommandRepository.Get(c => true).ToList();

            await _userCommandRepository.GetLastAsync(1234);

            commands.Should().NotBeNull();
            commands.Count.Should().Be(commandSet.Count);
            commands.Select(c => c.Type).Should().BeEquivalentTo(commandSet.Select(c => c.Type));
            ;
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
                    From = new User
                    {
                        Id = 1234,
                        IsBot = false,
                        FirstName = "Test",
                    },
                    Date = default,
                    OldChatMember = new ChatMemberMember(),
                    NewChatMember = new ChatMemberBanned(),
                    InviteLink = null,
                    ViaChatFolderInviteLink = null
                }
            };

            var updateHandlerFunc = async () => await _updateFirewall.HandleUpdateAsync(update);
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

            var updateHandlerFunc = async () => await _updateFirewall.HandleUpdateAsync(update);
            await updateHandlerFunc.Should().NotThrowAsync("Sticker message ignored");
        }

        [Test]
        public async Task SendTextMessage_NewUser_ReturnsGtpResponse()
        {
            var prompt = "What is the capital city of France?";
            var gtpResponse = "Paris";
            var response = new AssistantChatMessage(gtpResponse);

            _gptMock.Setup(m => m.SendMessage(It.Is<UpdateDecorator>(arg =>
                    arg.Message.Text.Equals(prompt)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var messageUpd = CreateTelegramUpdate(1, 2, prompt);
            var userId = messageUpd.Message.From.Id;
            await _updateFirewall.HandleUpdateAsync(messageUpd);

            _gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg =>
                    arg.Message.Text.Equals(prompt)
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            var message = _messageRepository.GetAllUserMessages(userId)
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

            for (var i = 0; i < RateLimiter.MaxMessagesCountPerMinute + 1; i++)
            {
                await _updateFirewall.HandleUpdateAsync(messageUpd);
            }

            _botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == messageUpd.Message.Chat.Id &&
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Exactly(2));

            _gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                It.IsAny<CancellationToken>()), Times.Exactly(RateLimiter.MaxMessagesCountPerMinute));
        }

        [Test]
        public async Task SendTextMessage_ManyParallelRequestsPerMinute_LimitExceeded()
        {
            var prompt = "SendTextMessage_ManyParallelRequestsPerMinute_LimitExceeded";
            var messageUpd = CreateTelegramUpdate(1, 2, prompt);

            var response = new SystemChatMessage("test");
            _gptMock.Setup(x => x.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                    It.IsAny<CancellationToken>()))
                .Returns(async (UpdateDecorator upd, CancellationToken token) =>
                {
                    // await Task.Delay(100, token);
                    return response;
                });

            for (var i = 0; i < RateLimiter.MaxMessagesCountPerMinute + 1; i++)
            {
                await _updateFirewall.HandleUpdateAsync(messageUpd);
            }

            _botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == messageUpd.Message!.Chat.Id &&
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                It.IsAny<CancellationToken>()), Times.Exactly(RateLimiter.MaxMessagesCountPerMinute));
        }

        [Test]
        public async Task SetBotUiLanguageCommand_RussianCulture_ReturnsChooseLanguageInstruction()
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ru");
            var update = CreateTelegramUpdate(1, 2, BotMenu.ChooseLangCommand);

            await _updateFirewall.HandleUpdateAsync(update);

            _botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == update.Message!.Chat.Id &&
                    arg.Text == BotResponse.ChooseLanguagePlease
                ),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task GenerateImageRequest_ManyRequests_ImagesPerDayLimitResponse()
        {
            var update = CreateTelegramUpdate(1, 2, "/image гора");

            for (var i = 0; i < ImageGeneratorHandler.ImagesPerDayLimit + 1; i++)
            {
                await _updateFirewall.HandleUpdateAsync(update);
            }

            var userId = update.Message!.From.Id;

            var generatedImagesCount = _messageRepository.GetTodayImagesCount(userId);

            generatedImagesCount.Should().Be(ImageGeneratorHandler.ImagesPerDayLimit);

            _botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
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
            await _updateFirewall.HandleUpdateAsync(getImageUpdate);

            var generatedImagesCount = _messageRepository.GetTodayImagesCount(getImageUpdate.Message!.From.Id);

            generatedImagesCount.Should().Be(1);
        }

        [Test]
        public async Task RecognizeImageTextRequest_ImageTextRecognizeCommand_ReturnsText()
        {
            var update = CreateTelegramUpdate(1, 2, BotMenu.ImageTextRecognizeCommand);
            var userId = update.Message!.From.Id;

            await _updateFirewall.HandleUpdateAsync(update);
            update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[]
            {
                new PhotoSize
                {
                    FileId = "test"
                }
            };

            await _updateFirewall.HandleUpdateAsync(update);

            _botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == userId &&
                    arg.Text == BotResponse.SendTextRecognitionImage
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _recognitionServiceMock.Verify(g => g.Recognize(It.IsAny<string>()),
                Times.Once);
        }

        [Test]
        public async Task RecognizeImageTextRequest_ImageMessageWithoutCommandRequest_ChooseCommandFirstResponse()
        {
            var update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[]
            {
                new PhotoSize
                {
                    FileId = "test"
                }
            };
            var userId = update.Message!.From.Id;

            await _updateFirewall.HandleUpdateAsync(update);

            var sendMessageRequestExpected = new SendMessageRequest(userId,
                BotResponse.SendImageTextRecognitionCommandFirst)
            {
                ReplyMarkup = TelegramBotUiService.CancelKeyboard
            };

            _botClientMock.Verify(b => b.MakeRequestAsync(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == sendMessageRequestExpected.ChatId &&
                    arg.Text == BotResponse.SendImageTextRecognitionCommandFirst
                ),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ResetContext_OldContextExists_ReturnNewContextId()
        {
            await _updateFirewall.HandleUpdateAsync(_startTelegramUpdate);
            var userId = _startTelegramUpdate.Message!.From.Id;
            var initialContextId = _messageRepository.GetLastContext(userId, userId);

            var resetContextUpdDecorator = CreateTelegramUpdate(1, 2, BotMenu.ResetContextCommand);
            await _updateFirewall.HandleUpdateAsync(resetContextUpdDecorator);

            var newContextId = _messageRepository.GetLastContext(userId, userId);

            newContextId.Should().NotBe(initialContextId);
        }

        [Test]
        public async Task SendMessage_ContextExists_SameContext()
        {
            await _updateFirewall.HandleUpdateAsync(_startTelegramUpdate);
            var userId = _startTelegramUpdate.Message!.From.Id;
            var initialContextId = _messageRepository.GetLastContext(userId, userId);

            var firstMessageUpd = CreateTelegramUpdate(1, 2, "first");
            await _updateFirewall.HandleUpdateAsync(firstMessageUpd);

            var secondMessageUpd = CreateTelegramUpdate(3, 4, "second");
            await _updateFirewall.HandleUpdateAsync(secondMessageUpd);

            var newContextId = _messageRepository.GetLastContext(userId, userId);

            newContextId.Should().Be(initialContextId);
        }

        [Test]
        public async Task StartCommand_UserNotExists_NewUserAdded()
        {
            var userRepository = _services.GetRequiredService<UserRepository>();

            await _updateFirewall.HandleUpdateAsync(_startTelegramUpdate);

            var newUser = userRepository.Get(TestConstants.UserId);

            newUser.Should().NotBeNull();
        }

        [Test]
        public async Task TextMessage_TwoTimes_UserCached()
        {
            var userRepository = _services.GetRequiredService<UserRepository>();

            await _updateFirewall.HandleUpdateAsync(_startTelegramUpdate);
            await _updateFirewall.HandleUpdateAsync(_startTelegramUpdate);

            var newUser = userRepository.Get(TestConstants.UserId);
            var cached = _memoryCache.Get<GPTipsBot.Models.User>("User_" + _startTelegramUpdate.Message.From.Id);
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