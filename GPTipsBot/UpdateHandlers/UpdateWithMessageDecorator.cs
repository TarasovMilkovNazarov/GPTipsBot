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
            TelegramGptMessage = new TelegramGptMessage(update.Message);
        }

        public Update Update { get; set; }
        public TelegramGptMessage TelegramGptMessage { get; set; }
        public CancellationToken StatusTimerCancellationToken { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }
}
