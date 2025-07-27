using System.Globalization;
using AutoFixture;
using dotenv.net;
using FluentAssertions;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.YandexCloud;
using GPTipsBot.UpdateHandlers;
using GPTipsBotTests.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using OpenAI.ObjectModels.ResponseModels;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Message = Telegram.Bot.Types.Message;
using User = Telegram.Bot.Types.User;

namespace GPTipsBotTests
{
    public class DispatcherTests
    {
        private readonly Update _startTelegramUpdate;
        private readonly IServiceCollection _serviceCollection;
        private IServiceProvider _services;
        private Mock<ITelegramBotClient> _botClientMock;
        private MessageRepository _messageRepository;
        private readonly Mock<IGpt> _gptMock;
        private readonly Mock<IImageGenerator> _imageGeneratorMock;
        private MainHandler _mainHandler;
        private readonly Mock<ITextRecognizer> _recognitionServiceMock;
        private readonly Fixture _fixture;
        private IMemoryCache _memoryCache;
        private UserCommandRepository _userCommandRepository;

        public DispatcherTests()
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
                .AddSingleton<IGpt>(_gptMock.Object)
                .AddSingleton(gramadsMockClient.Object);

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
                    Id = messageId,
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
            _botClientMock = new();
            _botClientMock.Setup(b => b.SendRequest(It.IsAny<GetFileRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new TGFile()
            {
                FileId = "test",
                FileSize = 1,
                FilePath = "test.txt"
            });

            _botClientMock.Setup(b => b.SendRequest(
                It.IsAny<SendMessageRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Message()
            {
                Id = 123
            });

            _services = _serviceCollection
                .AddSingleton(_botClientMock.Object)
                .BuildServiceProvider();
            ResetRequestsRateLimit();
            var appContext = _services.GetRequiredService<ApplicationContext>();
            await ClearDatabase(appContext);
            _messageRepository = _services.GetRequiredService<MessageRepository>();
            _userCommandRepository = _services.GetRequiredService<UserCommandRepository>();
            _mainHandler = _services.GetRequiredService<MainHandler>();
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
                await _mainHandler.HandleUpdateAsync(update);
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
                    ViaChatFolderInviteLink = false
                }
            };

            var updateHandlerFunc = async () => await _mainHandler.HandleUpdateAsync(update);
            await updateHandlerFunc.Should().NotThrowAsync("Kicked member just ignored");
        }

        [Test]
        [TestCase(MessageType.Sticker)]
        [TestCase(MessageType.ChannelChatCreated)]
        [TestCase(MessageType.NewChatTitle)]
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

            var updateHandlerFunc = async () => await _mainHandler.HandleUpdateAsync(update);
            await updateHandlerFunc.Should().NotThrowAsync("Sticker message ignored");
        }

        [Test]
        public async Task SendTextMessage_NewUser_ReturnsGtpResponse()
        {
            var prompt = "What is the capital city of France?";
            var gtpResponse = "Paris";
            var response = new ChatCompletionCreateResponse
            {
                Choices = new() { new() { Message = new("system", gtpResponse) } }
            };

            _gptMock.Setup(m => m.SendMessage(It.Is<UpdateDecorator>(arg =>
                    arg.Message.Text.Equals(prompt)), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var messageUpd = CreateTelegramUpdate(1, 2, prompt);
            var userId = messageUpd.Message.From.Id;
            await _mainHandler.HandleUpdateAsync(messageUpd);

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
                await _mainHandler.HandleUpdateAsync(messageUpd);
            }

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == messageUpd.Message.Chat.Id &&
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                It.IsAny<CancellationToken>()), Times.Exactly(RateLimiter.MaxMessagesCountPerMinute));
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
            _gptMock.Setup(x => x.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                    It.IsAny<CancellationToken>()))
                .Returns(async (UpdateDecorator upd, CancellationToken token) =>
                {
                    // await Task.Delay(100, token);
                    return response;
                });

            for (var i = 0; i < RateLimiter.MaxMessagesCountPerMinute + 1; i++)
            {
                await _mainHandler.HandleUpdateAsync(messageUpd);
            }

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
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

            await _mainHandler.HandleUpdateAsync(update);

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == update.Message!.Chat.Id &&
                    arg.Text == BotResponse.ChooseLanguagePlease
                ),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task GenerateImageRequest_NewbieSpentFreeRequests_TopUpBalanceMessage()
        {
            var update = CreateTelegramUpdate(1, 2, "/image гора");

            for (var i = 0; i < PaymentConfig.NewbieFreeImageGenerations + 1; i++)
            {
                await _mainHandler.HandleUpdateAsync(update);
            }

            var userId = update.Message!.From.Id;

            var generatedImagesCount = _messageRepository.GetTodayImagesCount(userId);

            generatedImagesCount.Should().Be(PaymentConfig.NewbieFreeImageGenerations);

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == userId &&
                    arg.Text == BotResponse.SimpleNoFreeRequests
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _imageGeneratorMock.Verify(g => g.GenerateImage("гора"),
                Times.Exactly(PaymentConfig.NewbieFreeImageGenerations));
        }

        [Test]
        public async Task GenerateImageRequest_ImageWithDescriptionCommand_GenerateImage()
        {
            var imagePromptWithCommand = "/image кракозябра";
            var getImageUpdate = CreateTelegramUpdate(1, 2, imagePromptWithCommand);
            await _mainHandler.HandleUpdateAsync(getImageUpdate);

            var generatedImagesCount = _messageRepository.GetTodayImagesCount(getImageUpdate.Message!.From.Id);

            generatedImagesCount.Should().Be(1);
        }

        [Test]
        public async Task RecognizeImageTextRequest_ImageTextRecognizeCommand_ReturnsText()
        {
            var update = CreateTelegramUpdate(1, 2, BotMenu.ImageTextRecognizeCommand);
            var userId = update.Message!.From.Id;

            await _mainHandler.HandleUpdateAsync(update);
            update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[]
            {
                new PhotoSize
                {
                    FileId = "test"
                }
            };

            await _mainHandler.HandleUpdateAsync(update);

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
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

            var imageUpdateFunc = async() => await _mainHandler.HandleUpdateAsync(update);

            await imageUpdateFunc.Should().ThrowExactlyAsync<NotSupportedMessageException>();
        }

        [Test]
        public async Task RecognizeImageTextRequest_CommandSelected_NextMessageImageExpected()
        {
            var commandUpdate = CreateTelegramUpdate(2, 2, BotMenu.ImageTextRecognizeCommand);
            var notImageUpdate = CreateTelegramUpdate(2, 2, null);

            await _mainHandler.HandleUpdateAsync(commandUpdate);
            await _mainHandler.HandleUpdateAsync(notImageUpdate);

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == notImageUpdate.Message!.Chat.Id &&
                    arg.Text == BotResponse.SendTextRecognitionImage
                ),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ResetContext_OldContextExists_ReturnNewContextId()
        {
            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);
            var userId = _startTelegramUpdate.Message!.From.Id;
            var initialContextId = _messageRepository.GetLastContext(userId, userId);

            var resetContextUpdDecorator = CreateTelegramUpdate(1, 2, BotMenu.ResetContextCommand);
            await _mainHandler.HandleUpdateAsync(resetContextUpdDecorator);

            var newContextId = _messageRepository.GetLastContext(userId, userId);

            newContextId.Should().NotBe(initialContextId);
        }

        [Test]
        public async Task SendMessage_ContextExists_SameContext()
        {
            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);
            var userId = _startTelegramUpdate.Message!.From.Id;
            var initialContextId = _messageRepository.GetLastContext(userId, userId);

            var firstMessageUpd = CreateTelegramUpdate(1, 2, "first");
            await _mainHandler.HandleUpdateAsync(firstMessageUpd);

            var secondMessageUpd = CreateTelegramUpdate(3, 4, "second");
            await _mainHandler.HandleUpdateAsync(secondMessageUpd);

            var newContextId = _messageRepository.GetLastContext(userId, userId);

            newContextId.Should().Be(initialContextId);
        }

        [Test]
        public async Task StartCommand_UserNotExists_NewUserAdded()
        {
            var userRepository = _services.GetRequiredService<UserRepository>();

            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);

            var newUser = userRepository.Get(TestConstants.UserId);

            newUser.Should().NotBeNull();
        }

        [Test]
        public async Task TextMessage_TwoTimes_UserCached()
        {
            var userRepository = _services.GetRequiredService<UserRepository>();

            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);
            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);

            var newUser = userRepository.Get(TestConstants.UserId);
            var cached = _memoryCache.Get<GPTipsBot.Models.User>
                (UserRepository.CacheKeyPrefix + _startTelegramUpdate.Message!.From.Id);
            cached.Should().BeEquivalentTo(newUser);
        }

        [Test]
        public async Task DepositCommand_WalletNotExists_BalanceChanged()
        {
            var walletRepository = _services.GetRequiredService<WalletRepository>();

            var balanceBefore = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault()?.Balance ?? 0;

            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);
            var starsToAdd = 10;

            var paymentUpdate = new Update
            {
                PreCheckoutQuery = new PreCheckoutQuery
                {
                    From = new User
                    {
                        Id = TestConstants.UserId
                    },
                    Currency = "XTR",
                    TotalAmount = starsToAdd,
                    InvoicePayload = string.Empty
                },
            };
            await _mainHandler.HandleUpdateAsync(paymentUpdate);

            var walletUpdated = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();

            walletUpdated.Should().NotBeNull();
            walletUpdated.Balance.Should().Be(balanceBefore + starsToAdd);
        }

        [Test]
        [TestCase("10", true)]
        [TestCase("-10", false)]
        [TestCase("some random text", false)]
        public async Task DepositCommand_UserInput_BalanceChanged(string input, bool isValid)
        {
            var walletRepository = _services.GetRequiredService<WalletRepository>();

            var balanceBefore = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault()?.Balance ?? 0;

            await _mainHandler.HandleUpdateAsync(CreateTelegramUpdate(1234, 1234, BotMenu.DepositCommand));

            var starsInputUpdateFunc = async () => await _mainHandler
                .HandleUpdateAsync(CreateTelegramUpdate(1234, 1234, input));

            if (!isValid)
            {
                await starsInputUpdateFunc.Should().ThrowExactlyAsync<ClientCanceledException>();
                return;
            }
            else
            {
                await starsInputUpdateFunc();
            }

            var paymentUpdate = new Update
            {
                PreCheckoutQuery = new PreCheckoutQuery
                {
                    From = new User
                    {
                        Id = TestConstants.UserId
                    },
                    Currency = "XTR",
                    TotalAmount = int.Parse(input),
                    InvoicePayload = string.Empty
                },
            };
            await _mainHandler.HandleUpdateAsync(paymentUpdate);

            var walletUpdated = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();

            walletUpdated.Should().NotBeNull();
            walletUpdated.Balance.Should().Be(balanceBefore + int.Parse(input));
        }

        [Test]
        public async Task InMemoryAdvertisement_InParallel_SentOnce()
        {
            var tracker = new InMemoryAdvertisementTracker(_botClientMock.Object);
            int successCount = 0;
            int attempts = 0;

            Parallel.For(0, 10, async _ => {
                Interlocked.Increment(ref attempts);
                if (await tracker.TrySendAdvertisement(TestConstants.UserId))
                {
                    Interlocked.Increment(ref successCount);
                }
            });

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == TestConstants.UserId &&
                    arg.Text == BotResponse.AdvertisementText
                ),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task DepositCommand_WalletExists_BalanceChanged()
        {
            await using var scope = _services.CreateAsyncScope();
            var walletRepository = scope.ServiceProvider.GetRequiredService<WalletRepository>();
            var userRepository = scope.ServiceProvider.GetRequiredService<UserRepository>();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();

            var user = new GPTipsBot.Models.User()
            {
                Id = TestConstants.UserId,
                FirstName = "Test"
            };
            await userRepository.Create(user);
            walletRepository.Create(new Wallet
            {
                CreatedAt = DateTime.UtcNow,
                User = user,
                Balance = 10,
                Currency = "XTR"
            });

            await context.SaveChangesAsync();

            var balanceBefore = walletRepository.Get(w => w.UserId == TestConstants.UserId).First().Balance;

            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);
            var starsToAdd = 10;

            var paymentUpdate = new Update
            {
                PreCheckoutQuery = new PreCheckoutQuery
                {
                    From = new User
                    {
                        Id = TestConstants.UserId
                    },
                    Currency = "XTR",
                    TotalAmount = starsToAdd,
                    InvoicePayload = string.Empty
                },
            };
            await _mainHandler.HandleUpdateAsync(paymentUpdate);

            var walletUpdated = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();

            walletUpdated.Should().NotBeNull();
            walletUpdated.Balance.Should().Be(balanceBefore + starsToAdd);
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