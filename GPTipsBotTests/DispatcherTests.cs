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
using Invoice = GPTipsBot.Models.Invoice;

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
            _imageGeneratorMock.Setup(s => s.StartImageGenerationAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync("test-operation-id");
            _imageGeneratorMock.Setup(s => s.GetImageGenerationStatusAsync(It.IsAny<string>()))
                .ReturnsAsync(new ImageGenerationStatus(true, TestConstants.GeneratedImageBase64));
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

            _botClientMock.Setup(b => b.SendRequest(
                It.IsAny<SendPhotoRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Message()
            {
                Id = 124
            });

            _botClientMock.Setup(b => b.SendRequest(
                It.IsAny<DeleteMessageRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _botClientMock.Setup(b => b.SendRequest(
                It.IsAny<AnswerPreCheckoutQueryRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(true);

            _botClientMock.Setup(b => b.SendRequest(
                It.IsAny<SendInvoiceRequest>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(new Message()
            {
                Id = 456
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

            var workflowHost = _services.GetRequiredService<WorkflowCore.Interface.IWorkflowHost>();
            workflowHost.RegisterWorkflow<GPTipsBot.Services.YandexCloud.Workflow.ImageGenerationWorkflow,
                GPTipsBot.Services.YandexCloud.Workflow.ImageGenerationWorkflowData>();
            workflowHost.RegisterWorkflow<GPTipsBot.Services.YandexPhotoAnimator.Workflow.PhotoAnimationWorkflow,
                GPTipsBot.Services.YandexPhotoAnimator.Workflow.PhotoAnimationWorkflowData>();
            workflowHost.Start();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _services?.GetRequiredService<WorkflowCore.Interface.IWorkflowHost>().Stop();
            }
            catch
            {
                // ignored: provider may already be disposed
            }
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
            _services.GetRequiredService<RateLimiter>().Reset();
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
        public async Task SendTextMessage_ParallelBurst_QueuesOneDropsRestWithoutLimitMessage()
        {
            const string prompt = "SendTextMessage_ParallelBurst_QueuesOneDropsRestWithoutLimitMessage";
            var response = new ChatCompletionCreateResponse
            {
                Choices = new() { new() { Message = new("system", "test") } }
            };

            var concurrent = 0;
            var maxConcurrent = 0;
            var gate = new object();

            _gptMock.Setup(x => x.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                    It.IsAny<CancellationToken>()))
                .Returns(async (UpdateDecorator _, CancellationToken token) =>
                {
                    var now = Interlocked.Increment(ref concurrent);
                    lock (gate)
                    {
                        if (now > maxConcurrent)
                        {
                            maxConcurrent = now;
                        }
                    }

                    try
                    {
                        await Task.Delay(300, token);
                        return response;
                    }
                    finally
                    {
                        Interlocked.Decrement(ref concurrent);
                    }
                });

            var updates = Enumerable.Range(1, 3)
                .Select(i => CreateTelegramUpdate(i, i + 10, prompt))
                .ToList();

            await Task.WhenAll(updates.Select(u => _mainHandler.HandleUpdateAsync(u)));

            maxConcurrent.Should().Be(1);
            _gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                It.IsAny<CancellationToken>()), Times.Exactly(2));

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task SendTextMessage_WhileBusy_ThirdMessageDroppedSilently()
        {
            const string prompt = "SendTextMessage_WhileBusy_ThirdMessageDroppedSilently";
            var response = new ChatCompletionCreateResponse
            {
                Choices = new() { new() { Message = new("system", "test") } }
            };

            var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var gptCalls = 0;

            _gptMock.Setup(x => x.SendMessage(It.Is<UpdateDecorator>(arg => arg.Message.Text.Equals(prompt)),
                    It.IsAny<CancellationToken>()))
                .Returns(async (UpdateDecorator _, CancellationToken token) =>
                {
                    var call = Interlocked.Increment(ref gptCalls);
                    if (call == 1)
                    {
                        firstEntered.TrySetResult();
                        await releaseFirst.Task.WaitAsync(token);
                    }

                    return response;
                });

            var first = _mainHandler.HandleUpdateAsync(CreateTelegramUpdate(1, 11, prompt));
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var second = _mainHandler.HandleUpdateAsync(CreateTelegramUpdate(2, 12, prompt));
            var third = _mainHandler.HandleUpdateAsync(CreateTelegramUpdate(3, 13, prompt));

            // Second is queued; third must be dropped before the first finishes.
            await Task.Delay(100);
            gptCalls.Should().Be(1);

            releaseFirst.TrySetResult();
            await Task.WhenAll(first, second, third);

            gptCalls.Should().Be(2);
            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.Text == BotResponse.TooManyRequests
                ),
                It.IsAny<CancellationToken>()), Times.Never);
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

            await WaitForTodayImagesCount(userId, PaymentConfig.NewbieFreeImageGenerations);

            var generatedImagesCount = _messageRepository.GetTodayImagesCount(userId);

            generatedImagesCount.Should().Be(PaymentConfig.NewbieFreeImageGenerations);

            // падает тк появилось форматированное сообщение с DateTime.UtcNow
            // todo добавить TimeProvider
            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.ChatId == userId &&
                    arg.Text == BotResponse.SimpleNoFreeRequests
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            if (AppConfig.IsProduction)
            {
                _imageGeneratorMock.Verify(g => g.StartImageGenerationAsync("гора", false),
                    Times.Exactly(PaymentConfig.NewbieFreeImageGenerations));
            }
        }

        [Test]
        public async Task GenerateImageRequest_ImageWithDescriptionCommand_GenerateImage()
        {
            var imagePromptWithCommand = "/image кракозябра";
            var getImageUpdate = CreateTelegramUpdate(1, 2, imagePromptWithCommand);
            await _mainHandler.HandleUpdateAsync(getImageUpdate);

            await WaitForTodayImagesCount(getImageUpdate.Message!.From.Id, expectedCount: 1);

            var generatedImagesCount = _messageRepository.GetTodayImagesCount(getImageUpdate.Message!.From.Id);

            generatedImagesCount.Should().Be(1);
        }

        [Test]
        public async Task GenerateImageRequest_PhraseRegexMisses_LlmFallbackGeneratesImage()
        {
            const string prompt = "хочу картинку с котом в очках";
            var llmResponse = new ChatCompletionCreateResponse
            {
                Choices = new()
                {
                    new() { Message = new("assistant", """{"intent": "generate_image", "prompt": "кот в очках"}""") }
                }
            };

            _gptMock.Setup(m => m.SendOneOffAsync(
                    It.IsAny<string>(),
                    It.Is<string>(s => s == prompt),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(llmResponse);

            var update = CreateTelegramUpdate(1, 2, prompt);
            await _mainHandler.HandleUpdateAsync(update);

            await WaitForTodayImagesCount(update.Message!.From.Id, expectedCount: 1);

            _imageGeneratorMock.Verify(g => g.StartImageGenerationAsync("кот в очках", false), Times.Once);
        }

        [Test]
        public async Task ChatMessage_LlmFallbackReturnsNone_FallsThroughToChat()
        {
            const string prompt = "как погода в москве сегодня";

            var update = CreateTelegramUpdate(1, 2, prompt);
            await _mainHandler.HandleUpdateAsync(update);

            _gptMock.Verify(g => g.SendMessage(It.Is<UpdateDecorator>(arg =>
                    arg.Message.Text.Equals(prompt)),
                It.IsAny<CancellationToken>()), Times.Once);
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
        public async Task PhotoWithoutCommand_ShowsImagesMenu()
        {
            var update = CreateTelegramUpdate(2, 2, null);
            update.Message!.Photo = new[]
            {
                new PhotoSize
                {
                    FileId = "test"
                }
            };

            await _mainHandler.HandleUpdateAsync(update);

            _botClientMock.Verify(b => b.SendRequest(It.Is<SendMessageRequest>(arg =>
                    arg.Text == BotResponse.ChooseImagesPlease
                ),
                It.IsAny<CancellationToken>()), Times.Once);
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
            var invoiceRepository = _services.GetRequiredService<InvoiceRepository>();

            var balanceBefore = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault()?.Balance ?? 0;

            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);
            var starsToAdd = 10;
            var invoice = await CreateInvoiceAsync(TestConstants.UserId, starsToAdd);

            await _mainHandler.HandleUpdateAsync(CreatePreCheckoutUpdate(invoice, starsToAdd));

            var walletAfterPreCheckout = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();
            (walletAfterPreCheckout?.Balance ?? 0).Should().Be(balanceBefore);

            await _mainHandler.HandleUpdateAsync(CreateSuccessfulPaymentUpdate(invoice, starsToAdd));

            var walletUpdated = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();

            walletUpdated.Should().NotBeNull();
            walletUpdated.Balance.Should().Be(balanceBefore + starsToAdd);
            invoiceRepository.GetById(invoice.Id)!.Status.Should().Be(InvoiceStatus.Paid);
        }

        [Test]
        [TestCase("10", true)]
        [TestCase("-10", false)]
        [TestCase("some random text", false)]
        public async Task DepositCommand_UserInput_BalanceChanged(string input, bool isValid)
        {
            var walletRepository = _services.GetRequiredService<WalletRepository>();
            var invoiceRepository = _services.GetRequiredService<InvoiceRepository>();

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

            var starsToAdd = int.Parse(input);
            var invoice = invoiceRepository.Get(i => i.UserId == TestConstants.UserId).Single();

            await _mainHandler.HandleUpdateAsync(CreatePreCheckoutUpdate(invoice, starsToAdd));

            var walletAfterPreCheckout = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();
            (walletAfterPreCheckout?.Balance ?? 0).Should().Be(balanceBefore);

            await _mainHandler.HandleUpdateAsync(CreateSuccessfulPaymentUpdate(invoice, starsToAdd));

            var walletUpdated = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();

            walletUpdated.Should().NotBeNull();
            walletUpdated.Balance.Should().Be(balanceBefore + starsToAdd);
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
            var invoice = await CreateInvoiceAsync(TestConstants.UserId, starsToAdd);

            await _mainHandler.HandleUpdateAsync(CreatePreCheckoutUpdate(invoice, starsToAdd));
            walletRepository.Get(w => w.UserId == TestConstants.UserId).First().Balance.Should().Be(balanceBefore);

            await _mainHandler.HandleUpdateAsync(CreateSuccessfulPaymentUpdate(invoice, starsToAdd));

            var walletUpdated = walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault();

            walletUpdated.Should().NotBeNull();
            walletUpdated.Balance.Should().Be(balanceBefore + starsToAdd);
        }

        [Test]
        public async Task DepositCommand_PreCheckoutOnly_BalanceNotChanged()
        {
            var walletRepository = _services.GetRequiredService<WalletRepository>();
            var invoiceRepository = _services.GetRequiredService<InvoiceRepository>();

            await _mainHandler.HandleUpdateAsync(_startTelegramUpdate);
            var starsToAdd = 10;
            var invoice = await CreateInvoiceAsync(TestConstants.UserId, starsToAdd);

            await _mainHandler.HandleUpdateAsync(CreatePreCheckoutUpdate(invoice, starsToAdd));

            walletRepository.Get(w => w.UserId == TestConstants.UserId).FirstOrDefault().Should().BeNull();
            invoiceRepository.GetById(invoice.Id)!.Status.Should().Be(InvoiceStatus.Created);
        }

        private async Task<Invoice> CreateInvoiceAsync(long userId, int starsCount)
        {
            var invoiceRepository = _services.GetRequiredService<InvoiceRepository>();
            var context = _services.GetRequiredService<ApplicationContext>();

            var invoice = new Invoice
            {
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                Amount = starsCount,
                Currency = Currency.Stars,
                Status = InvoiceStatus.Created
            };
            invoiceRepository.Create(invoice);
            await context.SaveChangesAsync();
            return invoice;
        }

        private static Update CreatePreCheckoutUpdate(Invoice invoice, int starsToAdd)
        {
            return new Update
            {
                PreCheckoutQuery = new PreCheckoutQuery
                {
                    Id = "pre_checkout_1",
                    From = new User
                    {
                        Id = invoice.UserId,
                        FirstName = "Test"
                    },
                    Currency = Currency.Stars,
                    TotalAmount = starsToAdd,
                    InvoicePayload = invoice.Id.ToString()
                },
            };
        }

        private static Update CreateSuccessfulPaymentUpdate(Invoice invoice, int starsToAdd)
        {
            return new Update
            {
                Message = new Message
                {
                    Id = 999,
                    From = new User
                    {
                        Id = invoice.UserId,
                        IsBot = false,
                        FirstName = "Aleksandr",
                        LastName = "Tarasov",
                        Username = "alanextar",
                        LanguageCode = "ru"
                    },
                    Date = DateTime.UtcNow,
                    Chat = new Chat
                    {
                        Id = invoice.UserId,
                        Type = ChatType.Private,
                        Username = "alanextar",
                        FirstName = "Aleksandr",
                        LastName = "Tarasov"
                    },
                    SuccessfulPayment = new SuccessfulPayment
                    {
                        Currency = Currency.Stars,
                        TotalAmount = starsToAdd,
                        InvoicePayload = invoice.Id.ToString(),
                        TelegramPaymentChargeId = "tg_charge_1",
                        ProviderPaymentChargeId = "provider_charge_1"
                    }
                }
            };
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

        private async Task WaitForTodayImagesCount(long userId, int expectedCount, int timeoutMs = 15000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (_messageRepository.GetTodayImagesCount(userId) >= expectedCount)
                {
                    return;
                }

                await Task.Delay(200);
            }
        }
    }
}