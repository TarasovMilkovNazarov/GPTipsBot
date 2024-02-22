using GPTipsBot.Enums;
using GPTipsBot.Localization;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Dtos.Commands
{
    public class SetEnglishLanguageCommand : ICommand, IRepliable
    {
        private readonly BotSettingsRepository botSettingsRepository;

        public string SlashCommand => CommandHelper.SetEngLanguage;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.SetEngLang);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.SetEngLanguage, Description = BotUI.SetEngLang };

        public string ReplyText => BotResponse.LanguageWasSetSuccessfully;

        public IReplyMarkup? ReplyKeyboard => new ReplyKeyboardRemove();

        public Task ApplyAsync(UpdateDecorator update)
        {
            CultureInfo.CurrentUICulture = LocalizationManager.En;
            update.Reply.Text = ReplyText;
            MainHandler.userState[update.UserChatKey].LanguageCode = LocalizationManager.En.Name;
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;

            var settings = botSettingsRepository.Get(update.UserChatKey.Id);
            if (settings == null)
            {
                botSettingsRepository.Create(update.UserChatKey.Id, LocalizationManager.En.Name);
            }
            else
            {
                botSettingsRepository.Update(update.UserChatKey.Id, LocalizationManager.En.Name);
            }

            return Task.CompletedTask;
        }

        public async Task ReplyAsync(ITelegramBotClient botClient, long chatId)
        {
            await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(chatId));
            await botClient.SendTextMessageAsync(chatId, ReplyText, replyMarkup: ReplyKeyboard);
        }

        public SetEnglishLanguageCommand(BotSettingsRepository botSettingsRepository)
        {
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.SetEngLang);
            this.botSettingsRepository = botSettingsRepository;
        }
    }
}
