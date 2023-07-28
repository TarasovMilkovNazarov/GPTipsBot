using GPTipsBot.Api;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Mapper;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    using static TelegramBotUIService;
    using static BotMenu;

    public class CommandHandler : BaseMessageHandler
    {
        private readonly MessageHandlerFactory messageHandlerFactory;
        private readonly MessageRepository messageContextRepository;
        private readonly UserRepository userRepository;
        private readonly UserService userService;
        private readonly ImageCreatorService imageCreatorService;
        private readonly InsightFaceSwapper faceSwapper;
        private readonly ITelegramBotClient botClient;
        private readonly BotSettingsRepository botSettingsRepository;
        private readonly TelegramBotAPI telegramBotAPI;
        private readonly ILogger<CommandHandler> logger;

        public CommandHandler(MessageHandlerFactory messageHandlerFactory, MessageRepository messageContextRepository,
            UserRepository userRepository, ITelegramBotClient botClient, BotSettingsRepository botSettingsRepository,
            TelegramBotAPI telegramBotAPI, ILogger<CommandHandler> logger, UserService userService, ImageCreatorService imageCreatorService,
            InsightFaceSwapper faceSwapper)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.messageContextRepository = messageContextRepository;
            this.userRepository = userRepository;
            this.botClient = botClient;
            this.botSettingsRepository = botSettingsRepository;
            this.telegramBotAPI = telegramBotAPI;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<UserStateHandler>());
            this.userService = userService;
            this.imageCreatorService = imageCreatorService;
            this.faceSwapper = faceSwapper;
        }

        public override async Task HandleAsync(UpdateDecorator update, CancellationToken cancellationToken)
        {
            var messageText = update.Message.Text;
            var chatId = update.UserChatKey.ChatId;

            if (!TryGetCommand(messageText, out var command))
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            IReplyMarkup? replyMarkup = startKeyboard;
            update.Message.ContextBound = false;

            switch (command)
            {
                case CommandType.Start:
                    await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(update.UserChatKey.ChatId));
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    update.User.Source = TelegramService.GetSource(messageText);
                    userService.CreateUpdateUser(UserMapper.Map(update.User));
                    update.Reply.Text = BotResponse.Greeting;
                    break;
                case CommandType.Help:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    update.Reply.Text = BotResponse.BotDescription;
                    break;
                case CommandType.CreateImage:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingImage;
                    if (messageText.StartsWith("/image "))
                    {
                        update.Message.Text = messageText.Substring("/image ".Length);
                        SetNextHandler(messageHandlerFactory.Create<UserStateHandler>());
                        await base.HandleAsync(update, cancellationToken);
                        return;
                    }
                    update.Reply.Text = String.Format(BotResponse.InputImageDescriptionText, ImageGeneratorHandler.basedOnExperienceInputLengthLimit);
                    replyMarkup = cancelKeyboard;
                    break;
                case CommandType.ResetContext:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    update.Reply.Text = BotResponse.ContextUpdated;
                    update.Message.NewContext = true;
                    break;
                case CommandType.Feedback:
                    update.Reply.Text = BotResponse.SendFeedback;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.SendingFeedback;
                    replyMarkup = cancelKeyboard;
                    break;
                case CommandType.ChooseLang:
                    update.Reply.Text = BotResponse.ChooseLanguagePlease;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingLanguage;
                    replyMarkup = chooseLangKeyboard;
                    break;
                case CommandType.SetEngLang:
                    await UpdateLanguage(update.UserChatKey, "en");
                    break;
                case CommandType.SetRuLang:
                    await UpdateLanguage(update.UserChatKey, "ru");
                    break;
                case CommandType.Cancel:
                    update.Reply.Text = BotResponse.Cancel;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    break;
                case CommandType.StopRequest:
                    update.Reply.Text = BotResponse.Cancel;
                    if (MainHandler.userState.ContainsKey(update.UserChatKey))
                    {
                        var state = MainHandler.userState[update.UserChatKey];
                        
                        if (state.CurrentState == UserStateEnum.None)
                        { 
                            replyMarkup = new ReplyKeyboardRemove();
                        }
                        else
                        {
                            replyMarkup = cancelKeyboard;
                        }

                        if (update.Message.TelegramMessageId.HasValue && state.messageIdToCancellation.ContainsKey(update.Message.TelegramMessageId.Value))
                        {
                            state.messageIdToCancellation[update.Message.TelegramMessageId.Value].Cancel();
                        }
                    }
                    break;
                case CommandType.Games:
                    update.Reply.Text = BotResponse.ChooseGame;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingGames;
                    replyMarkup = gamesKeyboard;
                    break;
                case CommandType.TickTackToe:
                    await SetGameInstructions(ChatGptGamesPrompts.TickTacToe, UserStateEnum.PlayingTickTacToe);
                    return;
                case CommandType.EmojiTranslation:
                    await SetGameInstructions(ChatGptGamesPrompts.EmojiTranslation, UserStateEnum.PlayingEmojiTranslations);
                    return;
                case CommandType.BookDivination:
                    await SetGameInstructions(ChatGptGamesPrompts.BookDivination, UserStateEnum.PlayingBookDivination);
                    return;
                case CommandType.GuessWho:
                    await SetGameInstructions(ChatGptGamesPrompts.GuessWho, UserStateEnum.PlayingGuessWho);
                    return;
                case CommandType.AdventureGame:
                    await SetGameInstructions(ChatGptGamesPrompts.Adventure, UserStateEnum.PlayingAdventureGame);
                    return;
                case CommandType.FaceSwap:
                    update.Reply.Text = BotResponse.FaceSwapInstruction;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingFaceSwapImages;
                    return;
            }

            if (!string.IsNullOrEmpty(update.Reply.Text))
            {
                messageContextRepository.AddMessage(update.Message);
                await botClient.SendTextMessageAsync(chatId, update.Reply.Text, cancellationToken: cancellationToken, replyMarkup: replyMarkup);
            }

            Task SetGameInstructions(string prompt, UserStateEnum userState)
            {
                update.Message.Text = prompt;
                update.Message.NewContext = true;
                update.Message.ContextBound = true;
                update.Message.Role = MessageOwner.System;
                MainHandler.userState[update.UserChatKey].CurrentState = userState;
                SetNextHandler(messageHandlerFactory.Create<MainHandler>());
                return base.HandleAsync(update, cancellationToken);
            }

            async Task UpdateLanguage(UserChatKey userKey, string langCode)
            {
                CultureInfo.CurrentUICulture = new CultureInfo(langCode);
                MainHandler.userState[userKey].LanguageCode = langCode;
                botSettingsRepository.Update(userKey.Id, langCode);
                update.Reply.Text = BotResponse.LanguageWasSetSuccessfully;
                await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(update.UserChatKey.ChatId));
                replyMarkup = new ReplyKeyboardRemove();
                MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
            }
        }

        private bool TryGetCommand(string message, out CommandType? command)
        {
            message = message.Trim().ToLower();

            if (message.StartsWith(Start.Command.ToLower()))
            {
                command = CommandType.Start;
            }
            else if (message.Equals(Help.Command.ToLower()) || ButtonToLocalizations[HelpStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.Help;
            }
            else if (message.StartsWith(Image.Command.ToLower()) || ButtonToLocalizations[ImageStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.CreateImage;
            }
            else if (message.StartsWith(UpdateBingCookieStr.ToLower()))
            {
                command = CommandType.UpdateBingCookie;
            }
            else if (message.Equals(ResetContext.Command.ToLower()) || ButtonToLocalizations[ResetContextStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.ResetContext;
            }
            else if (message.Equals(Feedback.Command.ToLower()) || ButtonToLocalizations[FeedbackStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.Feedback;
            }
            else if (message.Equals(ChooseLang.Command.ToLower()) || ButtonToLocalizations[ChooseLangStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.ChooseLang;
            }
            else if (message.Equals(SetRuLang.Command.ToLower()) || ButtonToLocalizations[SetRuLangStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.SetRuLang;
            }
            else if (message.Equals(SetEngLang.Command.ToLower()) || ButtonToLocalizations[SetEngLangStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.SetEngLang;
            }
            else if (message.Equals(CancelStr.ToLower()) || ButtonToLocalizations[CancelStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.Cancel;
            }
            else if (message.Equals(StopRequestStr.ToLower()))
            {
                command = CommandType.StopRequest;
            }
            else if (message.Equals(GamesStr.ToLower()) || ButtonToLocalizations[GamesStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.Games;
            }
            else if (message.Equals(TickTackToeStr.ToLower()) || ButtonToLocalizations[TickTackToeStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.TickTackToe;
            }
            else if (message.Equals(BookDivinationStr.ToLower()) || ButtonToLocalizations[BookDivinationStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.BookDivination;
            }
            else if (message.Equals(GuessWhoStr.ToLower()) || ButtonToLocalizations[GuessWhoStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.GuessWho;
            }
            else if (message.Equals(EmojiTranslationStr.ToLower()) || ButtonToLocalizations[EmojiTranslationStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.EmojiTranslation;
            }
            else if (message.Equals(AdventureStr.ToLower()) || ButtonToLocalizations[AdventureStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.AdventureGame;
            }
            else if (message.Equals(FaceSwapStr.ToLower()) || ButtonToLocalizations[FaceSwapStr].Any(b => b.ToLower() == message))
            {
                command = CommandType.AdventureGame;
            }
            else
            {
                command = null;
            }

            return command.HasValue;
        }
    }
}
