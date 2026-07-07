using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class AdminCommandHandler(
        ITelegramBotClient botClient,
        OpenAiVpnConnectivityService connectivityService) : BaseMessageHandler
    {
        public override async Task HandleAsync(UpdateDecorator update)
        {
            Guard.Against.Null(update.Message.Text);

            var chatKey = update.UserChatKey;
            switch (update.Message.Text)
            {
                case BotMenu.FixCommand when chatKey.IsAdmin():
                {
                    var response = AppConfig.IsOnMaintenance ? BotResponse.Recovered : BotResponse.OnMaintenance;
                    AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;
                    await botClient.SendMessage(chatKey.ChatId, response);
                    return;
                }
                case BotMenu.VersionCommand when chatKey.IsAdmin():
                    await botClient.SendBotVersionAsync(chatKey.ChatId);
                    return;
                case BotMenu.VpnCheckCommand when chatKey.IsAdmin():
                {
                    var result = await connectivityService.CheckAsync();
                    var message = OpenAiVpnConnectivityService.FormatResultMessage(result);
                    await botClient.SendMessage(chatKey.ChatId, message);
                    return;
                }
            }
        }
    }
}