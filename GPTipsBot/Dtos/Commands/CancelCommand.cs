using GPTipsBot.Enums;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Dtos.Commands
{
    public class CancelCommand : ICommand, IRepliable
    {
        public string SlashCommand => CommandHelper.Cancel;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.CancelButton);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.Cancel };

        public string ReplyText => BotResponse.Cancel;

        public IReplyMarkup ReplyKeyboard => CommandService.StartKeyboard;

        public Task ApplyAsync(UpdateDecorator update)
        {
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;

            return Task.CompletedTask;
        }

        public CancelCommand()
        {
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.CancelButton);
        }
    }
}
