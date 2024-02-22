using GPTipsBot.Enums;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Dtos.Commands
{
    public class ImageCommand : ICommand, IRepliable
    {
        public string SlashCommand => CommandHelper.Image;
        public List<string> CommandVariants { get; }
        public static KeyboardButton Button => new KeyboardButton(BotUI.ImageButton);
        public static BotCommand BotCommand => new BotCommand { Command = CommandHelper.Image, Description = BotUI.Image };

        private BaseMessageHandler Handler { get; }

        public string ReplyText => String.Format(BotResponse.InputImageDescriptionText,
                ImageGeneratorHandler.imageTextDescriptionLimit);

        public IReplyMarkup? ReplyKeyboard => CommandService.CancelKeyboard;

        public async Task ApplyAsync(UpdateDecorator update)
        {
            var message = update.Message.Text;
            MainHandler.userState[update.UserChatKey].CurrentState = UserStateEnum.AwaitingImage;
            if (message.StartsWith("/image "))
            {
                update.Message.Text = message.Substring("/image ".Length);
                await Handler.HandleAsync(update);
            }
        }

        public ImageCommand(HandlerFactory factory)
        {
            Handler = factory.Create<CrudHandler>();
            CommandVariants = CommandService.GetAllCommandLocalizations(() => BotUI.ImageButton);
        }
    }
}
