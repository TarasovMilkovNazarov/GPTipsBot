using GPTipsBot.Enums;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Dtos.Commands
{
    public class HelpCommand : ICommand, IRepliable
    {
        public string SlashCommand => CommandHelper.Help;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.HelpButton);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.Help, Description = BotUI.Help };

        public string ReplyText => BotResponse.BotDescription;

        public IReplyMarkup? ReplyKeyboard => CommandService.StartKeyboard;

        public Task ApplyAsync(UpdateDecorator update)
        {
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;

            return Task.CompletedTask;
        }

        public HelpCommand()
        {
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.HelpButton);
        }
    }
}
