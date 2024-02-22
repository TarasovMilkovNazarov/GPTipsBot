using GPTipsBot.Db;
using GPTipsBot.Dtos.Commands;
using GPTipsBot.Localization;
using GPTipsBot.Repositories;
using GPTipsBot.UpdateHandlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using System.Resources;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Services
{
    [JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
    public class BotMenu
    {
        public BotMenu()
        {
        }

        public BotCommand[] GetBotCommands()
        {
            return new BotCommand[]
            {
                StartCommand.BotCommand,
                ImageCommand.BotCommand,
                ResetContextCommand.BotCommand,
                HelpCommand.BotCommand,
            };
        }
    }

    public class CommandService
    {
        public static ReplyKeyboardMarkup StartKeyboard => GetMenuKeyboardMarkup();
        public static ReplyKeyboardMarkup CancelKeyboard => GetCancelKeyboardMarkup();
        public static ReplyKeyboardMarkup ChooseLangKeyboard => GetLanguageKeyboardMarkup();
        public static IReplyMarkup EmptyKeyboard => new ReplyKeyboardRemove();

        public List<ICommand> AllCommands { get; set; }

        public CommandService(HandlerFactory factory, UnitOfWork unitOfWork)
        {
            var cancel = new CancelCommand();
            var image = new ImageCommand(factory);
            var start = new StartCommand(unitOfWork.Messages);
            var help = new HelpCommand();
            var resetContext = new ResetContextCommand(unitOfWork.Messages);
            var selectLanguage = new SelectLanguageCommand(unitOfWork.Messages);
            var setRussianLanguage = new SetRussianLanguageCommand(unitOfWork.BotSettings);
            var setEnglishLanguage = new SetEnglishLanguageCommand(unitOfWork.BotSettings);
            var stop = new StopRequestCommand();

            AllCommands = new List<ICommand>() { 
                start,
                stop,
                cancel,
                help,
                resetContext,
                image,
                start,
                help,
                resetContext,
                selectLanguage,
                setRussianLanguage,
                setEnglishLanguage
            };
        }

        public bool TryGetCommand(string message, out ICommand? command)
        {
            message = message.Trim().ToLower();
            command = null;

            foreach (var cmd in AllCommands)
            {
                if (message.StartsWith(cmd.SlashCommand.ToLower()) ||  
                    cmd.CommandVariants.Exists(t => t.ToLower() == message))
                {
                    command = cmd;
                    return true;
                }
            }

            return false;
        }

        private static ReplyKeyboardMarkup GetMenuKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new[]
                {
                    ResetContextCommand.Button,
                    ImageCommand.Button
                },
                new[]
                {
                    HelpCommand.Button,
                    SelectLanguageCommand.Button
                }
            });

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }
        
        private static ReplyKeyboardMarkup GetCancelKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(CancelCommand.Button);

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }

        private static ReplyKeyboardMarkup GetLanguageKeyboardMarkup()
        {
            var keyboardMarkup = new ReplyKeyboardMarkup(new[]
                {
                    new[] 
                    { 
                        SetRussianLanguageCommand.Button, 
                        SetEnglishLanguageCommand.Button 
                    },
                    new[] 
                    { 
                        CancelCommand.Button
                    }
                }
            );

            keyboardMarkup.ResizeKeyboard = true;
            keyboardMarkup.OneTimeKeyboard = true;

            return keyboardMarkup;
        }

        public static List<string> GetAllCommandLocalizations(Func<string> getLocalizedValue)
        {
            var result = new List<string>();
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo.CurrentUICulture = LocalizationManager.En;
            result.Add(getLocalizedValue());

            CultureInfo.CurrentUICulture = LocalizationManager.Ru;
            result.Add(getLocalizedValue());
            CultureInfo.CurrentUICulture = currentCulture;

            return result;
        }
    }
}
