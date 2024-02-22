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
    public class SetRussianLanguageCommand : ICommand, IRepliable
    {
        private readonly BotSettingsRepository botSettingsRepository;

        public string SlashCommand => CommandHelper.SetRuLanguage;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.SetRuLang);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.SetRuLanguage, Description = BotUI.SetRuLang };

        public string ReplyText => BotResponse.LanguageWasSetSuccessfully;
        public IReplyMarkup? ReplyKeyboard => new ReplyKeyboardRemove();

        public Task ApplyAsync(UpdateDecorator update)
        {
            CultureInfo.CurrentUICulture = LocalizationManager.Ru;
            update.Reply.Text = ReplyText;
            MainHandler.userState[update.UserChatKey].LanguageCode = LocalizationManager.Ru.Name;
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
            var settings = botSettingsRepository.Get(update.UserChatKey.Id);
            if (settings == null)
            {
                botSettingsRepository.Create(update.UserChatKey.Id, LocalizationManager.Ru.Name);
            }
            else
            {
                botSettingsRepository.Update(update.UserChatKey.Id, LocalizationManager.Ru.Name);
            }

            return Task.CompletedTask;
        }

        public async Task ReplyAsync(ITelegramBotClient botClient, long chatId)
        {
            await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(chatId));
            await botClient.SendTextMessageAsync(chatId, ReplyText, replyMarkup: ReplyKeyboard);
        }

        public SetRussianLanguageCommand(BotSettingsRepository botSettingsRepository)
        {
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.SetRuLang);
            this.botSettingsRepository = botSettingsRepository;
        }
    }
}
