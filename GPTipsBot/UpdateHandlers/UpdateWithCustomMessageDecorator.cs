using GPTipsBot.Dtos;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace GPTipsBot.UpdateHandlers
{
    public class UpdateWithCustomMessageDecorator
    {
        public UpdateWithCustomMessageDecorator(Update update, CancellationToken cancellationToken)
        {
            Update = update;
            CancellationToken = cancellationToken;
            if (update.Message != null)
            {
                TelegramGptMessage = new TelegramGptMessageUpdate(update.Message);
            }
            else if (update.CallbackQuery?.Message != null)
            {
                TelegramGptMessage = new TelegramGptMessageUpdate(update.CallbackQuery);
                TelegramGptMessage.Text = update.CallbackQuery.Data;
            }
        }

        public Update Update { get; set; }
        public TelegramGptMessageUpdate? TelegramGptMessage { get; set; }
        public CancellationToken StatusTimerCancellationToken { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}
