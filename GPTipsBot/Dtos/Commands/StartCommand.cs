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
    public class StartCommand : ICommand, IRepliable
    {
        private readonly MessageRepository messageRepository;

        public string SlashCommand => CommandHelper.Start;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.Start);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.Start, Description = BotUI.Start };

        public string ReplyText => BotResponse.Greeting;
        public IReplyMarkup? ReplyKeyboard => CommandService.StartKeyboard;

        public Task ApplyAsync(UpdateDecorator update)
        {
            messageRepository.AddMessage(update.Message);
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.None;

            return Task.CompletedTask;
        }

        public async Task ReplyAsync(ITelegramBotClient botClient, long chatId)
        {
            await botClient.SetMyCommandsAsync(new BotMenu().GetBotCommands(), BotCommandScope.Chat(chatId));
            await botClient.SendTextMessageAsync(chatId, ReplyText, replyMarkup: ReplyKeyboard);
        }

        public StartCommand(MessageRepository messageRepository)
        {
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.Start);
            this.messageRepository = messageRepository;
        }
    }
}
