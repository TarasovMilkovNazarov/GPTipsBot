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
    public class ResetContextCommand : ICommand, IRepliable
    {
        private readonly MessageRepository messageRepository;

        public string SlashCommand => CommandHelper.ResetContext;
        public string ReplyText => BotResponse.ContextUpdated;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.ResetContextButton);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.ResetContext, Description = BotUI.ResetContext };

        public IReplyMarkup? ReplyKeyboard => CommandService.StartKeyboard;

        public Task ApplyAsync(UpdateDecorator update)
        {
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;
            update.Message.NewContext = true;
            messageRepository.AddMessage(update.Message);

            return Task.CompletedTask;
        }

        public ResetContextCommand(MessageRepository messageRepository)
        {
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.ResetContextButton);
            this.messageRepository = messageRepository;
        }
    }
}
