using System.Globalization;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GPTipsBot.UpdateHandlers
{
    public class MainHandler
    {
        private readonly Dispatcher _dispatcher;
        private readonly SpeechToTextService _speechToTextService;
        private readonly TelejetAdClient _telejetAdClient;
        private readonly RateLimiter _rateLimiter;
        private readonly ITelegramBotClient _botClient;
        private readonly ApplicationContext _context;
        private readonly BotCommandMenuService _botCommandMenuService;

        public static DateTime Start { get; private set; }

        public MainHandler(
            Dispatcher dispatcher,
            SpeechToTextService speechToTextService,
            TelejetAdClient telejetAdClient,
            RateLimiter rateLimiter,
            ITelegramBotClient botClient,
            ApplicationContext context,
            BotCommandMenuService botCommandMenuService)
        {
            _dispatcher = dispatcher;
            _botClient = botClient;
            _context = context;
            _speechToTextService = speechToTextService;
            _telejetAdClient = telejetAdClient;
            _rateLimiter = rateLimiter;
            _botCommandMenuService = botCommandMenuService;
            Start = DateTime.UtcNow;
        }

        public async Task HandleUpdateAsync(Update update)
        {
            var needHandleUpd = await _telejetAdClient.HandleUpdateAsync(update);
            if (!needHandleUpd)
            {
                return;
            }

            if (update.MyChatMember != null)
            {
                await TrySetGroupMenuOnJoinAsync(update.MyChatMember);
            }

            if (update.Ignore())
            {
                return;
            }

            var extendedUpd = new UpdateDecorator(update);
            var enter = _rateLimiter.TryEnter(extendedUpd);
            if (enter is ChatGateResult.Queued or ChatGateResult.Dropped)
            {
                return;
            }

            var ownsSlot = enter == ChatGateResult.ProcessNow;
            var chatId = extendedUpd.UserChatKey.ChatId;
            var current = extendedUpd;
            Exception? error = null;

            try
            {
                while (current != null)
                {
                    try
                    {
                        await HandleAllowedUpdateAsync(current);
                    }
                    catch (Exception ex)
                    {
                        error ??= ex;
                    }

                    if (!ownsSlot)
                    {
                        break;
                    }

                    current = _rateLimiter.Release(chatId);
                    if (current == null)
                    {
                        ownsSlot = false;
                    }
                }
            }
            finally
            {
                if (ownsSlot)
                {
                    _rateLimiter.ForceRelease(chatId);
                }
            }

            if (error != null)
            {
                throw error;
            }
        }

        private async Task HandleAllowedUpdateAsync(UpdateDecorator extendedUpd)
        {
            if (AppConfig.IsOnMaintenance)
            {
                await _botClient.SendMessage(extendedUpd.UserChatKey.ChatId, BotResponse.OnMaintenance);
                return;
            }

            if (extendedUpd.IsRecovered)
            {
                return;
            }

            if (extendedUpd.IsGroupOrChannel)
            {
                await _botCommandMenuService.EnsureGroupMenuAsync(extendedUpd.UserChatKey.ChatId);
            }

            if (extendedUpd.TelegramUpdate.Message?.Voice != null)
            {
                try
                {
                    extendedUpd.Message.Text =
                        await _speechToTextService.RecognizeVoice(extendedUpd.TelegramUpdate.Message.Voice.FileId);
                }
                catch (Exception)
                {
                    await _botClient.SendMessage(extendedUpd.UserChatKey.ChatId, BotResponse.SomethingWentWrong);
                    return;
                }
            }

            CultureInfo.CurrentUICulture = LocalizationManager.GetCulture(extendedUpd.Language);

            try
            {
                await _dispatcher.HandleAsync(extendedUpd);
            }
            finally
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task TrySetGroupMenuOnJoinAsync(ChatMemberUpdated myChatMember)
        {
            if (myChatMember.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
            {
                return;
            }

            if (myChatMember.NewChatMember.Status is not (ChatMemberStatus.Member or ChatMemberStatus.Administrator))
            {
                return;
            }

            await _botCommandMenuService.EnsureGroupMenuAsync(myChatMember.Chat.Id);
        }
    }
}
