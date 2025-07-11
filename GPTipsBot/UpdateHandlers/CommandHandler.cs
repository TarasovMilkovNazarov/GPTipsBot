using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Ardalis.GuardClauses;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    using static TelegramBotUiService;
    using static BotMenu;

    public class CommandHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly UnitOfWork unitOfWork;
        private readonly ILogger<CommandHandler> logger;
        private readonly MessageRepository messageRepository;
        private readonly UserCommandRepository userCommandRepository;
        private readonly ImageGeneratorHandler imageGeneratorHandler;

        public CommandHandler(ITelegramBotClient botClient,
            UnitOfWork unitOfWork, ILogger<CommandHandler> logger, MessageRepository messageRepository,
            UserCommandRepository userCommandRepository, ImageGeneratorHandler imageGeneratorHandler)
        {
            this.botClient = botClient;
            this.unitOfWork = unitOfWork;
            this.logger = logger;
            this.messageRepository = messageRepository;
            this.userCommandRepository = userCommandRepository;
            this.imageGeneratorHandler = imageGeneratorHandler;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var messageText = update.Message.Text;
            var chatId = update.UserChatKey.ChatId;

            if (!update.IsCommand)
            {
                await base.HandleAsync(update);
                return;
            }

            Guard.Against.Null(update.Command);
            var previousCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
            await userCommandRepository.AddAsync(update.UserChatKey, update.Command.Type);

            IReplyMarkup? replyMarkup = StartKeyboard;
            update.Message.ContextBound = false;
            string? reply = null;

            switch (update!.Command.Command)
            {
                case StartCommand:
                    await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(),
                        BotCommandScope.Chat(update.UserChatKey.ChatId));
                    reply = BotResponse.Greeting;
                    break;
                case HelpCommand:
                    reply = BotResponse.BotDescription;
                    break;
                case ImageCommand:
                    if (messageText.StartsWith("/image "))
                    {
                        update.Message.Text = messageText.Substring("/image ".Length);
                        SetNextHandler(imageGeneratorHandler);
                        await messageRepository.AddAsync(update.Message);
                        await base.HandleAsync(update);
                        return;
                    }
                    reply = String.Format(BotResponse.InputImageDescriptionText, ImageGeneratorHandler.ImageTextDescriptionLimit);
                    replyMarkup = CancelKeyboard;
                    break;
                case ImageTextRecognizeCommand:
                    reply = BotResponse.SendTextRecognitionImage;
                    replyMarkup = CancelKeyboard;
                    break;
                case ResetContextCommand:
                    reply = BotResponse.ContextUpdated;
                    update.Message.NewContext = true;
                    break;
                case ChooseLangCommand:
                    reply = BotResponse.ChooseLanguagePlease;
                    replyMarkup = ChooseLangKeyboard;
                    break;
                case SetEngLangCommand:
                    reply = await UpdateLanguage(update.UserChatKey, "en");
                    break;
                case SetRuLangCommand:
                    reply = await UpdateLanguage(update.UserChatKey, "ru");
                    break;
                case CancelCommand:
                    reply = BotResponse.Cancel;
                    break;
                case StopRequestCommand:
                    reply = BotResponse.Cancel;
                    if (!MainHandler.UserState.TryGetValue(update.UserChatKey, out var state))
                    {
                        break;
                    }

                    if (previousCommand?.Type is CommandType.Image or CommandType.TextRecognition)
                    {
                        replyMarkup = CancelKeyboard;
                    }
                    else
                    {
                        replyMarkup = new ReplyKeyboardRemove();
                    }

                    if (update.Message.TelegramMessageId.HasValue && state.MessageIdToCancellation
                            .ContainsKey(update.Message.TelegramMessageId.Value))
                    {
                        state.MessageIdToCancellation[update.Message.TelegramMessageId.Value].Cancel();
                    }

                    break;
            }

            Guard.Against.Null(reply);

            await unitOfWork.Messages.AddAsync(update.Message);
            await botClient.SendTextMessageAsync(chatId, reply, replyMarkup: replyMarkup);
            return;

            async Task<string?> UpdateLanguage(UserChatKey userKey, string langCode)
            {
                CultureInfo.CurrentUICulture = new CultureInfo(langCode);

                await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(update.UserChatKey.ChatId));
                replyMarkup = new ReplyKeyboardRemove();

                var settings = unitOfWork.BotSettings.Get(userKey.Id);
                if (settings == null)
                {
                    unitOfWork.BotSettings.Create(userKey.Id, langCode);
                }
                else
                {
                    unitOfWork.BotSettings.Update(userKey.Id, langCode);
                }

                return BotResponse.LanguageWasSetSuccessfully;
            }
        }
    }
}
