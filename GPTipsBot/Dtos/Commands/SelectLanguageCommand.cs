using GPTipsBot.Enums;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Dtos.Commands
{
    public class SelectLanguageCommand : ICommand, IRepliable
    {
        private readonly MessageRepository messageRepository;

        public string SlashCommand => CommandHelper.SelectLanguage;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.LangButton);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.SelectLanguage, Description = BotUI.SetLang };

        public string ReplyText => BotResponse.ChooseLanguagePlease;

        public IReplyMarkup? ReplyKeyboard => CommandService.ChooseLangKeyboard;

        public Task ApplyAsync(UpdateDecorator update)
        {
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingLanguage;
            messageRepository.AddMessage(update.Message);

            return Task.CompletedTask;
        }

        public async Task ReplyAsync(ITelegramBotClient botClient, long chatId)
        {
            await botClient.SendTextMessageAsync(chatId, ReplyText, replyMarkup: ReplyKeyboard);
        }

        public SelectLanguageCommand(MessageRepository messageRepository)
        {
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.LangButton);
            this.messageRepository = messageRepository;
        }
    }
}
