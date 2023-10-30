using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Utilities;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    public class OnAdminCommandHandler : BaseMessageHandler
    {
        private readonly ITelegramBotClient botClient;
        private readonly ImageCreatorService imageCreatorService;

        public OnAdminCommandHandler(
            MessageHandlerFactory messageHandlerFactory,
            ITelegramBotClient botClient,
            ImageCreatorService imageCreatorService)
        {
            this.botClient = botClient;
            SetNextHandler(messageHandlerFactory.Create<RateLimitingHandler>());
            this.imageCreatorService = imageCreatorService;
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            if (string.IsNullOrEmpty(update.Message.Text))
            {
                await base.HandleAsync(update);
                return;
            }

            var chatKey = update.UserChatKey;
            if (update.Message.Text == "/fix" && chatKey.IsAdmin() && AppConfig.IsOnMaintenance)
            {
                AppConfig.IsOnMaintenance = false;
                await botClient.SendTextMessageAsync(chatKey.ChatId, BotResponse.Recovered);
                return;
            }

            if (update.Message.Text == "/version" && chatKey.IsAdmin())
            {
                await botClient.SendTextMessageAsync(chatKey.ChatId, $"""
Version: {StringUtilities.EscapeTextForMarkdown2(AppConfig.Version)}
CommitHash: [{AppConfig.CommitHash}](https://github.com/TarasovMilkovNazarov/GPTipsBot/commit/{AppConfig.CommitHash})
""", null, ParseMode.MarkdownV2);
                return;
            }


            if (AppConfig.IsOnMaintenance)
            {
                await botClient.SendTextMessageAsync(chatKey.ChatId, BotResponse.OnMaintenance);
                return;
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}