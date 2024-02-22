using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.Dtos.Commands
{
    public interface IRepliable
    {
        public string ReplyText { get; }
        public IReplyMarkup? ReplyKeyboard { get; }
        public virtual async Task ReplyAsync(ITelegramBotClient botClient, long chatId)
        {
            await botClient.SendTextMessageAsync(chatId, ReplyText, replyMarkup: ReplyKeyboard);
        }
    }
}