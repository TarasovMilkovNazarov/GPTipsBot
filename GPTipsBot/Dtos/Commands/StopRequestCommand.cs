using GPTipsBot.Resources;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Dtos.Commands
{
    public class StopRequestCommand : ICommand, IRepliable
    {
        public string SlashCommand => CommandHelper.StopRequest;
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.StopRequest, Description = BotUI.Image };

        public string ReplyText => BotResponse.Cancel;

        public List<string> CommandVariants { get; }

        public IReplyMarkup? ReplyKeyboard => new ReplyKeyboardRemove();

        public Task ApplyAsync(UpdateDecorator update)
        {
            if (MainHandler.userState.ContainsKey(update.UserChatKey))
            {
                var state = MainHandler.userState[update.UserChatKey];

                if (update.Message.TelegramMessageId.HasValue && state.messageIdToCancellation.ContainsKey(update.Message.TelegramMessageId.Value))
                {
                    state.messageIdToCancellation[update.Message.TelegramMessageId.Value].Cancel();
                }
            }

            return Task.CompletedTask;
        }

        public StopRequestCommand()
        {
            CommandVariants = new List<string>();
        }
    }
}
