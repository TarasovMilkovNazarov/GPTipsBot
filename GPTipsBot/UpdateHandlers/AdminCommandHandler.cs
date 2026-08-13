using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Dtos;
using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.Services.Broadcast;
using Telegram.Bot;

namespace GPTipsBot.UpdateHandlers
{
    public class AdminCommandHandler(
        ITelegramBotClient botClient,
        OpenAiVpnConnectivityService connectivityService,
        BroadcastDraftStore draftStore,
        BroadcastService broadcastService,
        BroadcastRunner broadcastRunner)
        : BaseMessageHandler
    {
        private const string HelpText =
            """
            📢 Рассылка (только админам)

            /broadcast — начать черновик, следующим сообщением пришлите текст
            Можно сразу:
            /broadcast ваш текст

            Несколько языков в одном сообщении:
            ru:
            текст для русских

            en:
            text for others

            Команды:
            /broadcast preview — прислать варианты себе
            /broadcast test — только админам
            /broadcast start — всем из аудитории
            /broadcast status
            /broadcast stop
            /broadcast parse plain|html|markdown|markdownv2
            /broadcast silent on|off
            /broadcast lang all|ru,en,es

            Лимиты Telegram: 4096 символов, ~30 сообщ/сек (шлём 25).
            Язык получателя: BotSettings, иначе fallback (ru).
            """;

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var chatKey = update.UserChatKey;
            if (!chatKey.IsAdmin())
            {
                return;
            }

            if (update.CallbackQuery != null && BroadcastCallbacks.IsMatch(update.CallbackQuery.Data))
            {
                await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                await HandleCallbackAsync(update, update.CallbackQuery.Data!);
                return;
            }

            Guard.Against.Null(update.Message.Text);
            var text = update.Message.Text;

            if (update.Command?.Command == BotMenu.FixCommand || text == BotMenu.FixCommand)
            {
                var response = AppConfig.IsOnMaintenance ? BotResponse.Recovered : BotResponse.OnMaintenance;
                AppConfig.IsOnMaintenance = !AppConfig.IsOnMaintenance;
                await botClient.SendMessage(chatKey.ChatId, response);
                return;
            }

            if (update.Command?.Command == BotMenu.VersionCommand || text == BotMenu.VersionCommand)
            {
                await botClient.SendBotVersionAsync(chatKey.ChatId);
                return;
            }

            if (text == BotMenu.VpnCheckCommand)
            {
                var result = await connectivityService.CheckAsync();
                var message = OpenAiVpnConnectivityService.FormatResultMessage(result);
                await botClient.SendMessage(chatKey.ChatId, message);
                return;
            }

            if (update.Command?.Command == BotMenu.BroadcastCommand)
            {
                await HandleBroadcastCommandAsync(update);
                return;
            }

            if (draftStore.IsAwaitingText(chatKey.TelegramUserId ?? chatKey.Id) &&
                !update.IsCommand)
            {
                await ApplyTextAsync(update, text);
            }
        }

        private async Task HandleBroadcastCommandAsync(UpdateDecorator update)
        {
            var argument = "";
            UpdateDecorator.TryGetCommandArgument(update.Message.Text, BotMenu.BroadcastCommand, out argument);

            if (string.IsNullOrWhiteSpace(argument) ||
                argument.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                var draft = draftStore.GetOrCreate(AdminId(update));
                draft.AwaitingText = true;
                await botClient.SendMessage(
                    update.UserChatKey.ChatId,
                    HelpText,
                    replyMarkup: BroadcastService.ActionKeyboard());
                return;
            }

            var parts = argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var verb = parts[0].ToLowerInvariant();
            var rest = parts.Length > 1 ? parts[1].Trim() : "";

            switch (verb)
            {
                case "preview":
                    await PreviewAsync(update);
                    return;
                case "test":
                    await StartAsync(update, adminsOnly: true);
                    return;
                case "start":
                    await StartAsync(update, adminsOnly: false);
                    return;
                case "status":
                    await StatusAsync(update);
                    return;
                case "stop":
                    await StopAsync(update);
                    return;
                case "cancel":
                    draftStore.Clear(AdminId(update));
                    await botClient.SendMessage(update.UserChatKey.ChatId, "Черновик сброшен.");
                    return;
                case "parse":
                    await SetParseModeAsync(update, rest);
                    return;
                case "silent":
                    await SetSilentAsync(update, rest);
                    return;
                case "lang":
                    await SetLanguagesAsync(update, rest);
                    return;
                default:
                    await ApplyTextAsync(update, argument);
                    return;
            }
        }

        private async Task HandleCallbackAsync(UpdateDecorator update, string data)
        {
            switch (data)
            {
                case BroadcastCallbacks.Preview:
                    await PreviewAsync(update);
                    break;
                case BroadcastCallbacks.Test:
                    await StartAsync(update, adminsOnly: true);
                    break;
                case BroadcastCallbacks.Start:
                    await StartAsync(update, adminsOnly: false);
                    break;
                case BroadcastCallbacks.Status:
                    await StatusAsync(update);
                    break;
                case BroadcastCallbacks.Stop:
                    await StopAsync(update);
                    break;
                case BroadcastCallbacks.Cancel:
                    draftStore.Clear(AdminId(update));
                    await botClient.SendMessage(update.UserChatKey.ChatId, "Черновик сброшен.");
                    break;
            }
        }

        private async Task ApplyTextAsync(UpdateDecorator update, string raw)
        {
            var draft = draftStore.GetOrCreate(AdminId(update));
            var parsed = BroadcastTextParser.Parse(raw, draft.Config.FallbackLanguage);
            draft.Config.Texts = parsed;
            draft.AwaitingText = false;

            var error = BroadcastTextParser.Validate(draft.Config);
            if (error != null)
            {
                draft.AwaitingText = true;
                await botClient.SendMessage(update.UserChatKey.ChatId, error);
                return;
            }

            await SendDraftSummaryAsync(update, draft.Config);
        }

        private async Task SetParseModeAsync(UpdateDecorator update, string value)
        {
            var mode = value.Trim().ToLowerInvariant();
            if (mode is not ("plain" or "html" or "markdown" or "md" or "markdownv2" or "mdv2"))
            {
                await botClient.SendMessage(update.UserChatKey.ChatId, "parse: plain | html | markdown | markdownv2");
                return;
            }

            var draft = draftStore.GetOrCreate(AdminId(update));
            draft.Config.ParseModeName = mode is "md" ? "markdown" : mode is "mdv2" ? "markdownv2" : mode;
            await SendDraftSummaryAsync(update, draft.Config);
        }

        private async Task SetSilentAsync(UpdateDecorator update, string value)
        {
            var draft = draftStore.GetOrCreate(AdminId(update));
            draft.Config.DisableNotification = value.Equals("on", StringComparison.OrdinalIgnoreCase)
                                               || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                                               || value == "1";
            await SendDraftSummaryAsync(update, draft.Config);
        }

        private async Task SetLanguagesAsync(UpdateDecorator update, string value)
        {
            var draft = draftStore.GetOrCreate(AdminId(update));
            if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                draft.Config.Audience.Languages = null;
            }
            else
            {
                draft.Config.Audience.Languages = value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(LocalizationManager.NormalizeLanguage)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            await SendDraftSummaryAsync(update, draft.Config);
        }

        private async Task PreviewAsync(UpdateDecorator update)
        {
            var config = await RequireTextAsync(update);
            if (config == null)
            {
                return;
            }

            foreach (var (lang, text) in config.Texts.OrderBy(x => x.Key))
            {
                await botClient.SendMessage(
                    update.UserChatKey.ChatId,
                    $"— preview {lang} —\n{text}");
            }
        }

        private async Task StartAsync(UpdateDecorator update, bool adminsOnly)
        {
            var config = await RequireTextAsync(update);
            if (config == null)
            {
                return;
            }

            if (broadcastRunner.IsRunning)
            {
                await botClient.SendMessage(update.UserChatKey.ChatId, "Уже идёт другая рассылка. /broadcast stop или status.");
                return;
            }

            config.Audience.AdminsOnly = adminsOnly;
            var campaign = await broadcastService.CreateCampaignAsync(AdminId(update), config, CancellationToken.None);
            if (!broadcastRunner.TryEnqueue(campaign.Id))
            {
                await botClient.SendMessage(update.UserChatKey.ChatId, "Не удалось поставить рассылку в очередь.");
                return;
            }

            var mode = adminsOnly ? "тест админам" : "всем";
            await botClient.SendMessage(
                update.UserChatKey.ChatId,
                $"Запускаю рассылку #{campaign.Id} ({mode}), получателей: {campaign.TotalTargeted}.");
        }

        private async Task StatusAsync(UpdateDecorator update)
        {
            var running = await broadcastService.GetRunningAsync(CancellationToken.None);
            if (running != null)
            {
                await botClient.SendMessage(update.UserChatKey.ChatId, BroadcastService.FormatProgress(running));
                return;
            }

            var draft = draftStore.Get(AdminId(update));
            if (draft == null || draft.Config.Texts.Count == 0)
            {
                await botClient.SendMessage(update.UserChatKey.ChatId, "Нет активной рассылки и черновика. /broadcast");
                return;
            }

            await SendDraftSummaryAsync(update, draft.Config);
        }

        private async Task StopAsync(UpdateDecorator update)
        {
            if (!broadcastRunner.IsRunning)
            {
                await botClient.SendMessage(update.UserChatKey.ChatId, "Сейчас ничего не отправляется.");
                return;
            }

            broadcastRunner.RequestStop();
            await botClient.SendMessage(update.UserChatKey.ChatId, "Останавливаю рассылку…");
        }

        private async Task<BroadcastCampaignConfig?> RequireTextAsync(UpdateDecorator update)
        {
            var draft = draftStore.Get(AdminId(update));
            var error = draft == null ? "Сначала пришлите текст: /broadcast" : BroadcastTextParser.Validate(draft.Config);
            if (error == null)
            {
                return draft!.Config;
            }

            await botClient.SendMessage(update.UserChatKey.ChatId, error);
            return null;
        }

        private async Task SendDraftSummaryAsync(UpdateDecorator update, BroadcastCampaignConfig config)
        {
            var byLanguage = await broadcastService.CountByLanguageAsync(config, CancellationToken.None);
            var total = byLanguage.Values.Sum();
            await botClient.SendMessage(
                update.UserChatKey.ChatId,
                BroadcastService.FormatDraftSummary(config, byLanguage, total),
                replyMarkup: BroadcastService.ActionKeyboard());
        }

        private static long AdminId(UpdateDecorator update) =>
            update.UserChatKey.TelegramUserId ?? update.UserChatKey.Id;
    }
}
