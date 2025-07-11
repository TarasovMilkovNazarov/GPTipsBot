using System.Text.RegularExpressions;
using Ardalis.GuardClauses;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class GroupMessageHandler : BaseMessageHandler
    {
        private readonly UserCommandRepository userCommandRepository;

        public GroupMessageHandler(CommandHandler commandHandler, UserCommandRepository userCommandRepository)
        {
            this.userCommandRepository = userCommandRepository;
            SetNextHandler(commandHandler);
        }

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var message = update.Message;

            // @GPTipBot /start any text
            var pattern1 = @$"(@{AppConfig.BotName})\s(\/.*?)\s?(.*)";
            var match1 = Regex.Matches(message.Text, pattern1).FirstOrDefault();

            if (match1 != null)
            {
                update.Message.Text = message.Text.Remove(0,match1.Groups[1].Length);

                await base.HandleAsync(update);
                return;
            }

            // image@GPTipBot text image description
            var pattern2 = @$"(\/.*?)(@{AppConfig.BotName})\s?(.*)";
            var match2 = Regex.Matches(message.Text, pattern2).FirstOrDefault();

            if (match2 != null)
            {
                update.Message.Text = message.Text.Remove(match2.Groups[2].Index,match2.Groups[2].Length);

                await base.HandleAsync(update);
                return;
            }

            var botMentionedEntity = message?.EntityValues?.FirstOrDefault(ev => ev.Contains(AppConfig.BotName));
            var isBotMentioned = message?.Entities?.FirstOrDefault()?.Type == MessageEntityType.Mention && botMentionedEntity != null;
            var isReplyToBotMessage = message?.ReplyToMessage?.From?.IsBot ?? false;

            var previousCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
            var isUserWaitingResponse = previousCommand?.Type is CommandType.TextRecognition or CommandType.Image;

            switch (isBotMentioned)
            {
                case false when !isReplyToBotMessage && update.IsGroupOrChannel && !isUserWaitingResponse:
                    return;
                case true:
                    Guard.Against.Null(botMentionedEntity, nameof(botMentionedEntity));
                    update.Message.Text = update.Message.Text[botMentionedEntity.Length..].Trim();
                    break;
            }

            // Call next handler
            await base.HandleAsync(update);
        }
    }
}
