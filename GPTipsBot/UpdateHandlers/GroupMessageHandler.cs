using System.Text.RegularExpressions;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class GroupMessageHandler : BaseMessageHandler
    {
        public GroupMessageHandler(MessageHandlerFactory messageHandlerFactory)
        {
            SetNextHandler(messageHandlerFactory.Create<MessageTypeHandler>());
        }

        public override async Task HandleAsync(UpdateWithCustomMessageDecorator update, CancellationToken cancellationToken)
        {
            var message = update.Update.Message;

            // @GPTipBot /start any text
            string pattern1 = @"(@GPTips?Bot)\s(\/.*?)\s?(.*)";
            var match1 = Regex.Matches(message.Text, pattern1).FirstOrDefault();

            if (match1 != null)
            {
                update.TelegramGptMessage.Text = message.Text.Remove(0,match1.Groups[0].Length);

                await base.HandleAsync(update, cancellationToken);
                return;
            }

            // image@GPTipBot text image description
            string pattern2 = @"(\/.*?)(@GPTips?Bot)\s?(.*)";
            var match2 = Regex.Matches(message.Text, pattern2).FirstOrDefault();

            if (match2 != null)
            {
                update.TelegramGptMessage.Text = message.Text.Remove(match2.Groups[2].Index,match2.Groups[2].Length);

                await base.HandleAsync(update, cancellationToken);
                return;
            }

            if (message == null)
            {
                await base.HandleAsync(update, cancellationToken);
                return;
            }

            var botMentionedEntity = message?.EntityValues?.FirstOrDefault(ev => ev.ToLower().Contains("gptip"));
            var isBotMentioned = message.Entities?.FirstOrDefault()?.Type == MessageEntityType.Mention && botMentionedEntity != null;
            var isReplyToBotMessage = message.ReplyToMessage?.From?.IsBot ?? false;
            var groupOrChannelTypes = new ChatType?[] { ChatType.Supergroup, ChatType.Group, ChatType.Channel };
            var isGroupOrChannel = groupOrChannelTypes.Contains(message.Chat.Type);
            var isUserWaitingResponse = MainHandler.userState[update.TelegramGptMessage.UserKey].CurrentState != Enums.UserStateEnum.None;

            if (!isBotMentioned && !isReplyToBotMessage && isGroupOrChannel && !isUserWaitingResponse)
            {
                return;
            }

            if (isBotMentioned)
            {
                message.Text = message.Text.Substring(botMentionedEntity.Length).Trim();
                update.TelegramGptMessage.Text = message.Text;
            }

            // Call next handler
            await base.HandleAsync(update, cancellationToken);
        }
    }
}
