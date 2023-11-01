using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
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
        private readonly ITelegramBotClient botClient;
        private readonly UnitOfWork unitOfWork;
        private readonly ILogger<CommandHandler> logger;

        public CommandHandler(MessageHandlerFactory messageHandlerFactory, ITelegramBotClient botClient, 
            UnitOfWork unitOfWork, ILogger<CommandHandler> logger)
        {
            this.messageHandlerFactory = messageHandlerFactory;
            this.botClient = botClient;
            this.unitOfWork = unitOfWork;
            this.logger = logger;
            SetNextHandler(messageHandlerFactory.Create<CrudHandler>());
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var messageText = update.Message.Text;
            var chatId = update.UserChatKey.ChatId;

            if (!TryGetCommand(messageText, out var command))
            {
                await base.HandleAsync(update);
                return;
            }

            IReplyMarkup? replyMarkup = startKeyboard;
            update.Message.ContextBound = false;

            switch (command!.Command)
            {
                case StartCommand:
                    await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(update.UserChatKey.ChatId));
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    update.Reply.Text = BotResponse.Greeting;
                    break;
                case HelpCommand:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    update.Reply.Text = BotResponse.BotDescription;
                    break;
                case ImageCommand:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingImage;
                    if (messageText.StartsWith("/image "))
                    {
                        update.Message.Text = messageText.Substring("/image ".Length);
                        SetNextHandler(messageHandlerFactory.Create<CrudHandler>());
                        await base.HandleAsync(update);
                        return;
                    }
                    update.Reply.Text = String.Format(BotResponse.InputImageDescriptionText, ImageGeneratorHandler.imageTextDescriptionLimit);
                    replyMarkup = cancelKeyboard;
                    break;
                case ResetContextCommand:
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    update.Reply.Text = BotResponse.ContextUpdated;
                    update.Message.NewContext = true;
                    break;
                case FeedbackCommand:
                    update.Reply.Text = BotResponse.SendFeedback;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.SendingFeedback;
                    replyMarkup = cancelKeyboard;
                    break;
                case ChooseLangCommand:
                    update.Reply.Text = BotResponse.ChooseLanguagePlease;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingLanguage;
                    replyMarkup = chooseLangKeyboard;
                    break;
                case SetEngLangCommand:
                    await UpdateLanguage(update.UserChatKey, "en");
                    break;
                case SetRuLangCommand:
                    await UpdateLanguage(update.UserChatKey, "ru");
                    break;
                case CancelCommand:
                    update.Reply.Text = BotResponse.Cancel;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
                    break;
                case StopRequestCommand:
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
                case GamesCommand:
                    update.Reply.Text = BotResponse.ChooseGame;
                    MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingGames;
                    replyMarkup = gamesKeyboard;
                    break;
                case TickTackToeCommand:
                    await SetGameInstructions(ChatGptGamesPrompts.TickTacToe, UserStateEnum.PlayingTickTacToe);
                    return;
                case EmojiTranslationCommand:
                    await SetGameInstructions(ChatGptGamesPrompts.EmojiTranslation, UserStateEnum.PlayingEmojiTranslations);
                    return;
                case BookDivinationCommand:
                    await SetGameInstructions(ChatGptGamesPrompts.BookDivination, UserStateEnum.PlayingBookDivination);
                    return;
                case GuessWhoCommand:
                    await SetGameInstructions(ChatGptGamesPrompts.GuessWho, UserStateEnum.PlayingGuessWho);
                    return;
                case AdventureCommand:
                    await SetGameInstructions(ChatGptGamesPrompts.Adventure, UserStateEnum.PlayingAdventureGame);
                    return;
            }

            if (!string.IsNullOrEmpty(update.Reply.Text))
            {
                unitOfWork.Messages.AddMessage(update.Message);
                await botClient.SendTextMessageAsync(chatId, update.Reply.Text, replyMarkup: replyMarkup);
            }

            Task SetGameInstructions(string prompt, UserStateEnum userState)
            {
                update.Message.Text = prompt;
                update.Message.NewContext = true;
                update.Message.ContextBound = true;
                update.Message.Role = MessageOwner.System;
                MainHandler.userState[update.UserChatKey].CurrentState = userState;
                SetNextHandler(messageHandlerFactory.Create<MainHandler>());
                return base.HandleAsync(update);
            }

            async Task UpdateLanguage(UserChatKey userKey, string langCode)
            {
                CultureInfo.CurrentUICulture = new CultureInfo(langCode);
                MainHandler.userState[userKey].LanguageCode = langCode;

                update.Reply.Text = BotResponse.LanguageWasSetSuccessfully;
                await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(update.UserChatKey.ChatId));
                replyMarkup = new ReplyKeyboardRemove();
                MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;

                var settings = unitOfWork.BotSettings.Get(userKey.Id);
                if (settings == null)
                {
                    unitOfWork.BotSettings.Create(userKey.Id, langCode);
                }
                else
                {
                    unitOfWork.BotSettings.Update(userKey.Id, langCode);
                }
            }
        }

        private bool TryGetCommand(string message, out BotCommand? command)
        {
            message = message.Trim().ToLower();

            Type classType = typeof(BotMenu);
            var properties = classType.GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(BotCommand));

            foreach (PropertyInfo property in properties)
            {
                BotCommand? botCommand = property.GetValue(null) as BotCommand;

                if (botCommand == null) continue;

                var slashCommandValue = botCommand.Command;

                if (message.StartsWith(slashCommandValue.ToLower()) || 
                    (ButtonToLocalizations.ContainsKey(slashCommandValue) && 
                        ButtonToLocalizations[slashCommandValue].Exists(b => b.ToLower() == message)))
                {
                    command = botCommand;
                    return true;
                }
            }

            command = null;

            return false;
        }
    }
}
