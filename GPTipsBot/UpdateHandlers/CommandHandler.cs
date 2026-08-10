using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Jobs;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Services.Cache;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace GPTipsBot.UpdateHandlers
{
    using static TelegramBotUiService;
    using static BotMenu;

    public class CommandHandler(
        ITelegramBotClient botClient,
        ApplicationContext context,
        ILogger<CommandHandler> logger,
        MessageRepository messageRepository,
        UserCommandRepository userCommandRepository,
        ImageGeneratorHandler imageGeneratorHandler,
        GptImageHandler gptImageHandler,
        ChatGptHandler chatGptHandler,
        InvoiceRepository invoiceRepository,
        UserService userService,
        BotSettingsRepository botSettingsRepository,
        MoneyService moneyService,
        IJobService jobService,
        IGpt gptService,
        IGptImageSessionCache gptImageSessionCache)
        : BaseMessageHandler
    {
        private readonly ApplicationContext _context = context;
        private readonly ILogger<CommandHandler> _logger = logger;
        private readonly InvoiceRepository _invoiceRepository = invoiceRepository;
        private readonly MoneyService _moneyService = moneyService;

        public override async Task HandleAsync(UpdateDecorator update)
        {
            var messageText = update.Message.Text;
            var chatId = update.UserChatKey.ChatId;

            if (!update.IsCommand)
            {
                await base.HandleAsync(update);
                return;
            }

            Guard.Against.Null(update.Command);

            if (update.IsGroupOrChannel && !BotMenu.IsAllowedInGroup(update.Command.Type))
            {
                await botClient.SendMessage(
                    chatId,
                    BotResponse.GroupCommandNotAvailable,
                    replyParameters: update.Message.TelegramMessageId is long mid
                        ? new ReplyParameters { MessageId = (int)mid }
                        : null);
                return;
            }

            var previousCommand = await userCommandRepository.GetLastAsync(update.UserChatKey);
            await userCommandRepository.AddAsync(update.UserChatKey, update.Command.Type);

            var profile = await userService.GetUserProfile(update.UserChatKey.Id);

            ReplyMarkup? replyMarkup = GetMenuMarkup(update.IsGroupOrChannel);
            update.Message.ContextBound = false;
            string? reply = null;

            switch (update!.Command.Command)
            {
                case StartCommand:
                    await botClient.SetMyCommands(
                        update.IsGroupOrChannel
                            ? new BotMenu().GetGroupBotCommands()
                            : new BotMenu().GetBotCommands(),
                        BotCommandScope.Chat(chatId));
                    reply = BotResponse.Greeting;
                    break;
                case GetProfileCommand:
                    reply = string.Format(BotResponse.ProfileResponse, profile.FirstName,
                        profile.LastName, profile.Stars, profile.GptRequests, profile.Images, profile.ImageTexts,
                        profile.PhotoAnimations, profile.Summaries, profile.GptModelDisplayName);
                    replyMarkup = GetProfileInlineKeyboard();
                    break;
                case ImagesMenuCommand:
                    reply = BotResponse.ChooseImagesPlease;
                    replyMarkup = GetImagesMenuInlineKeyboard();
                    break;
                case ModelCommand:
                    await HandleModelCommandAsync(update, profile);
                    return;
                case GptImageCommand:
                    if (UpdateDecorator.TryGetCommandArgument(messageText, GptImageCommand, out var gptImagePrompt))
                    {
                        var session = gptImageSessionCache.GetOrCreate(update.UserChatKey.Id);
                        session.Mode = GptImageMode.Generate;
                        gptImageSessionCache.Set(update.UserChatKey.Id, session);
                        update.Message.Text = gptImagePrompt;
                        SetNextHandler(gptImageHandler);
                        await base.HandleAsync(update);
                        return;
                    }

                    await HandleGptImageStartAsync(update, GptImageMode.Generate);
                    return;
                case EditImageCommand:
                    await HandleGptImageStartAsync(update, GptImageMode.Edit);
                    return;
                case GptImageSizeSquareCommand:
                case GptImageSizeLandscapeCommand:
                case GptImageSizePortraitCommand:
                case GptImageQualityLowCommand:
                case GptImageQualityMediumCommand:
                case GptImageQualityHighCommand:
                    await HandleGptImageOptionAsync(update);
                    return;
                case DepositCommand:
                {
                    var depositText = string.Format(
                        BotResponse.DepositResponse,
                        PaymentConfig.MinRechargeRub,
                        PaymentConfig.MinRechargeStars);
                    var packagesKeyboard = moneyService.BuildDepositPackagesKeyboard();
                    if (update.CallbackQuery == null)
                    {
                        await botClient.SendMessage(chatId, depositText, replyMarkup: packagesKeyboard);
                    }
                    else
                    {
                        await botClient.EditMessageText(chatId, (int)update.Message.TelegramMessageId!,
                            depositText,
                            replyMarkup: packagesKeyboard);
                    }

                    return;
                }
                case DonateCommand:
                    await botClient.SendMessage(chatId, BotResponse.DonateInstructions, replyMarkup: CancelInlineKeyboard);
                    return;
                case HelpCommand:
                    reply = BotResponse.HelpText;
                    break;
                case SummaryCommand:
                    await HandleSummaryAsync(update);
                    return;
                case AskCommand:
                {
                    if (!UpdateDecorator.TryGetCommandArgument(messageText, AskCommand, out var question))
                    {
                        await botClient.SendMessage(
                            chatId,
                            BotResponse.AskUsage,
                            replyParameters: update.Message.TelegramMessageId is long askMid
                                ? new ReplyParameters { MessageId = (int)askMid }
                                : null);
                        return;
                    }

                    update.Message.Text = question;
                    update.Message.ContextBound = true;
                    SetNextHandler(chatGptHandler);
                    await base.HandleAsync(update);
                    return;
                }
                case ImageCommand:
                    if (profile is { Images: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessage(update);
                        return;
                    }

                    if (UpdateDecorator.TryGetCommandArgument(messageText, ImageCommand, out var imagePrompt))
                    {
                        update.Message.Text = imagePrompt;
                        SetNextHandler(imageGeneratorHandler);
                        await messageRepository.AddAsync(update.Message);
                        await base.HandleAsync(update);
                        return;
                    }

                    reply = string.Format(
                        BotResponse.InputImageDescriptionText,
                        ImageGeneratorHandler.ImageTextDescriptionLimit);
                    replyMarkup = GetImageInstructionInlineKeyboard(false);
                    break;
                case AnimatePhotoCommand:
                    await botClient.SendMessage(chatId, BotResponse.SendPhotoToAnimate, replyMarkup: CancelInlineKeyboard);
                    return;
                case ImageSquareCommand:
                    if (previousCommand?.Type == CommandType.ImageSquare)
                    {
                        return;
                    }
                    await botClient.EditMessageReplyMarkup(update.UserChatKey.ChatId,
                        (int)update.Message.TelegramMessageId!.Value,
                        replyMarkup: GetImageInstructionInlineKeyboard(true));
                    return;
                case ImageRectangleCommand:
                    if (previousCommand?.Type != CommandType.ImageSquare)
                    {
                        return;
                    }
                    await botClient.EditMessageReplyMarkup(update.UserChatKey.ChatId,
                        (int)update.Message.TelegramMessageId!.Value,
                        replyMarkup: GetImageInstructionInlineKeyboard(false));
                    return;
                case ImageTextRecognizeCommand:
                    if (profile is { ImageTexts: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessage(update);
                        return;
                    }

                    reply = BotResponse.SendTextRecognitionImage;
                    replyMarkup = CancelInlineKeyboard;
                    break;
                case PromptFromImageCommand:
                    if (profile is { GptRequests: <= 0, Stars: <= 0 })
                    {
                        await SendNoFreeRequestsMessage(update);
                        return;
                    }

                    reply = BotResponse.SendPromptFromImagePhoto;
                    replyMarkup = CancelInlineKeyboard;
                    break;
                case ResetContextCommand:
                    reply = BotResponse.ContextUpdated;
                    update.Message.NewContext = true;
                    break;
                case ChooseLangCommand:
                    reply = BotResponse.ChooseLanguagePlease;
                    replyMarkup = GetChooseLangMarkup(update.IsGroupOrChannel);
                    break;
                case SetEngLangCommand:
                    reply = await UpdateLanguage(update.UserChatKey, "en");
                    break;
                case SetRuLangCommand:
                    reply = await UpdateLanguage(update.UserChatKey, "ru");
                    break;
                case CancelCommand:
                    if (update.CallbackQuery == null)
                    {
                        reply = BotResponse.ContinueConversation;
                    }
                    else if (update.Message.TelegramMessageId.HasValue)
                    {
                        await botClient.EditMessageText(update.UserChatKey.ChatId,
                            (int)update.Message.TelegramMessageId.Value, BotResponse.ContinueConversation);
                        return;
                    };

                    break;
                case StopRequestCommand:
                    reply = BotResponse.Cancel;
                    if (!Dispatcher.UserState.TryGetValue(update.UserChatKey, out var state))
                    {
                        break;
                    }

                    if (previousCommand?.Type is CommandType.Image or CommandType.TextRecognition or CommandType.PromptFromImage)
                    {
                        replyMarkup = GetCancelMarkup(update.IsGroupOrChannel);
                    }
                    else if (!update.IsGroupOrChannel)
                    {
                        replyMarkup = new ReplyKeyboardRemove();
                    }
                    else
                    {
                        replyMarkup = null;
                    }

                    if (update.Message.TelegramMessageId.HasValue && state.MessageIdToCancellation
                            .ContainsKey(update.Message.TelegramMessageId.Value))
                    {
                        state.MessageIdToCancellation[update.Message.TelegramMessageId.Value].Cancel();
                    }

                    break;
            }

            Guard.Against.Null(reply);

            await messageRepository.AddAsync(update.Message);
            await botClient.SendMessage(chatId, reply, replyMarkup: replyMarkup);
            return;

            async Task<string?> UpdateLanguage(UserChatKey userKey, string langCode)
            {
                CultureInfo.CurrentUICulture = new CultureInfo(langCode);

                await botClient.SetMyCommands(
                    update.IsGroupOrChannel
                        ? new BotMenu().GetGroupBotCommands()
                        : new BotMenu().GetBotCommands(),
                    BotCommandScope.Chat(chatId));
                replyMarkup = update.IsGroupOrChannel ? null : new ReplyKeyboardRemove();

                var settings = botSettingsRepository.Get(userKey.Id);
                if (settings == null)
                {
                    botSettingsRepository.Create(userKey.Id, langCode);
                }
                else
                {
                    botSettingsRepository.Update(userKey.Id, langCode);
                }

                return BotResponse.LanguageWasSetSuccessfully;
            }
        }

        private async Task HandleGptImageStartAsync(UpdateDecorator update, GptImageMode mode)
        {
            var chatId = update.UserChatKey.ChatId;
            var session = gptImageSessionCache.GetOrCreate(update.UserChatKey.Id);
            session.Mode = mode;
            if (mode == GptImageMode.Generate)
            {
                session.ImageFileId = null;
            }

            gptImageSessionCache.Set(update.UserChatKey.Id, session);

            var text = mode == GptImageMode.Edit
                ? string.Format(BotResponse.GptImageEditIntro, session.StarsCost)
                : string.Format(BotResponse.GptImageGenerateIntro, session.StarsCost);

            var keyboard = mode == GptImageMode.Edit
                ? CancelInlineKeyboard
                : GetGptImageOptionsKeyboard(session);

            if (update.CallbackQuery != null && update.Message.TelegramMessageId.HasValue)
            {
                await botClient.EditMessageText(
                    chatId,
                    (int)update.Message.TelegramMessageId.Value,
                    text,
                    replyMarkup: keyboard);
            }
            else
            {
                await botClient.SendMessage(chatId, text, replyMarkup: keyboard);
            }
        }

        private async Task HandleGptImageOptionAsync(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;
            var session = gptImageSessionCache.GetOrCreate(update.UserChatKey.Id);
            // Keep generate/edit mode when toggling size/quality.

            switch (update.Command!.Command)
            {
                case GptImageSizeSquareCommand:
                    session.Size = GptImageConfig.SizeSquare;
                    break;
                case GptImageSizeLandscapeCommand:
                    session.Size = GptImageConfig.SizeLandscape;
                    break;
                case GptImageSizePortraitCommand:
                    session.Size = GptImageConfig.SizePortrait;
                    break;
                case GptImageQualityLowCommand:
                    session.Quality = GptImageConfig.QualityLow;
                    break;
                case GptImageQualityMediumCommand:
                    session.Quality = GptImageConfig.QualityMedium;
                    break;
                case GptImageQualityHighCommand:
                    session.Quality = GptImageConfig.QualityHigh;
                    break;
            }

            gptImageSessionCache.Set(update.UserChatKey.Id, session);

            var text = session.Mode == GptImageMode.Edit
                ? string.Format(BotResponse.GptImageSendEditPrompt, session.StarsCost)
                : string.Format(BotResponse.GptImageGenerateIntro, session.StarsCost);
            var keyboard = GetGptImageOptionsKeyboard(session);

            if (update.CallbackQuery != null && update.Message.TelegramMessageId.HasValue)
            {
                await botClient.EditMessageText(
                    chatId,
                    (int)update.Message.TelegramMessageId.Value,
                    text,
                    replyMarkup: keyboard);
            }
            else
            {
                await botClient.SendMessage(chatId, text, replyMarkup: keyboard);
            }
        }

        private async Task HandleModelCommandAsync(UpdateDecorator update, UserProfileDto profile)
        {
            var chatId = update.UserChatKey.ChatId;
            var language = botSettingsRepository.Get(update.UserChatKey.Id)?.Language
                           ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            if (UpdateDecorator.TryGetCommandArgument(update.Message.Text, ModelCommand, out var modelIdRaw))
            {
                var model = GptModelCatalog.Find(modelIdRaw.Trim());
                if (model is null)
                {
                    await botClient.SendUserReplyAsync(update, BotResponse.UnknownModel);
                    return;
                }

                if (!model.AllowFreeQuota && profile.Stars < model.StarsCost)
                {
                    var text = string.Format(
                        BotResponse.ModelNeedsBalance,
                        model.DisplayName,
                        model.StarsCost,
                        GptModelCatalog.Default.DisplayName);
                    var needsBalanceKeyboard = GetModelNeedsBalanceKeyboard();

                    if (update.CallbackQuery != null && update.Message.TelegramMessageId.HasValue)
                    {
                        await botClient.EditMessageText(
                            chatId,
                            (int)update.Message.TelegramMessageId.Value,
                            text,
                            replyMarkup: needsBalanceKeyboard);
                    }
                    else
                    {
                        await botClient.SendMessage(chatId, text, replyMarkup: needsBalanceKeyboard);
                    }

                    return;
                }

                botSettingsRepository.SetPreferredGptModel(update.UserChatKey.Id, model.Id, language);

                var selectedText = string.Format(BotResponse.ModelSelected, model.DisplayName, model.StarsCost);
                var keyboard = profile.Stars > 0
                    ? GetModelSelectionKeyboard(model.Id)
                    : GetModelNeedsBalanceKeyboard();

                if (update.CallbackQuery != null && update.Message.TelegramMessageId.HasValue)
                {
                    await botClient.EditMessageText(
                        chatId,
                        (int)update.Message.TelegramMessageId.Value,
                        selectedText,
                        replyMarkup: keyboard);
                }
                else
                {
                    await botClient.SendMessage(chatId, selectedText, replyMarkup: keyboard);
                }

                return;
            }

            await SendModelPickerOrDepositAsync(update, profile);
        }

        private async Task SendModelPickerOrDepositAsync(
            UpdateDecorator update,
            UserProfileDto profile)
        {
            var chatId = update.UserChatKey.ChatId;
            var current = userService.GetPreferredGptModel(update.UserChatKey.Id);

            if (profile.Stars <= 0)
            {
                var text = string.Format(
                    BotResponse.ModelSelectionRequiresBalance,
                    GptModelCatalog.Default.DisplayName);
                var keyboard = GetModelNeedsBalanceKeyboard();

                if (update.CallbackQuery != null && update.Message.TelegramMessageId.HasValue)
                {
                    await botClient.EditMessageText(
                        chatId,
                        (int)update.Message.TelegramMessageId.Value,
                        text,
                        replyMarkup: keyboard);
                }
                else
                {
                    await botClient.SendMessage(chatId, text, replyMarkup: keyboard);
                }

                return;
            }

            var pickerText = string.Format(
                BotResponse.ChooseModel,
                current.DisplayName,
                current.StarsCost,
                GptModelCatalog.Default.DisplayName);
            var pickerKeyboard = GetModelSelectionKeyboard(current.Id);

            if (update.CallbackQuery != null && update.Message.TelegramMessageId.HasValue)
            {
                await botClient.EditMessageText(
                    chatId,
                    (int)update.Message.TelegramMessageId.Value,
                    pickerText,
                    replyMarkup: pickerKeyboard);
            }
            else
            {
                await botClient.SendMessage(chatId, pickerText, replyMarkup: pickerKeyboard);
            }
        }

        private async Task HandleSummaryAsync(UpdateDecorator update)
        {
            var chatId = update.UserChatKey.ChatId;
            var messageThreadId = update.Message.MessageThreadId;

            var dayMessages = messageRepository.GetChatMessagesForDay(chatId, DateTime.UtcNow, messageThreadId);
            dayMessages = dayMessages
                .Where(m => m.Text is not null
                            && !m.Text.StartsWith(SummaryCommand, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (dayMessages.Count == 0)
            {
                await botClient.SendUserReplyAsync(update, BotResponse.SummaryEmpty);
                return;
            }

            var hold = await userService.TryReserveSummaryAsync(update.UserChatKey.Id);
            if (hold is null)
            {
                await botClient.SendMessage(
                    chatId,
                    BotResponse.SimpleNoFreeRequests,
                    messageThreadId: messageThreadId is long tid ? (int)tid : null,
                    replyMarkup: DepositInlineKeyboard,
                    replyParameters: update.Message.TelegramMessageId is long mid
                        ? new ReplyParameters { MessageId = (int)mid }
                        : null);
                return;
            }

            var transcript = BuildTranscript(dayMessages);
            var prompt = string.Format(BotResponse.SummaryPrompt, transcript);
            var confirmed = false;

            try
            {
                var response = await gptService.SendOneOffAsync(
                    "You are a helpful assistant that summarizes chat conversations.",
                    prompt,
                    CancellationToken.None);

                var summaryText = response.Choices.FirstOrDefault()?.Message.Content;
                if (string.IsNullOrWhiteSpace(summaryText))
                {
                    await botClient.SendUserReplyAsync(update, BotResponse.SomethingWentWrong);
                    return;
                }

                await messageRepository.AddAsync(new MessageDto(update.UserChatKey)
                {
                    Text = summaryText,
                    Role = MessageOwner.Assistant,
                    ContextBound = false,
                    BotMessageType = BotMessageType.ChatGptPrompt,
                    MessageThreadId = messageThreadId,
                });

                await botClient.SendUserReplyAsync(
                    update,
                    $"{BotResponse.SummaryHeader}\n\n{summaryText}");

                await userService.ConfirmAsync(hold.Id);
                confirmed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build day summary for chat {ChatId}", chatId);
                await botClient.SendUserReplyAsync(update, BotResponse.SomethingWentWrong);
            }
            finally
            {
                if (!confirmed)
                {
                    await userService.ReleaseAsync(hold.Id);
                }
            }
        }

        private static string BuildTranscript(IReadOnlyList<Models.Message> messages)
        {
            var sb = new StringBuilder();
            foreach (var message in messages)
            {
                var role = message.Role switch
                {
                    MessageOwner.User => $"user:{message.UserId}",
                    MessageOwner.Assistant => "assistant",
                    _ => message.Role.ToString().ToLowerInvariant()
                };
                var text = message.Text!.Length > 500 ? message.Text[..500] + "…" : message.Text;
                sb.AppendLine($"[{message.CreatedAt:HH:mm}] {role}: {text}");
            }

            const int maxChars = 24000;
            if (sb.Length > maxChars)
            {
                return sb.ToString(sb.Length - maxChars, maxChars);
            }

            return sb.ToString();
        }

        private async Task SendNoFreeRequestsMessage(UpdateDecorator update)
        {
            var nextRefreshLimitExec = await jobService.GetNextExecutionForExistingJob<RefreshFreeLimitsJob>();
            await botClient.SendOutOfFreeRequestsMessageAsync(update.UserChatKey.ChatId, nextRefreshLimitExec);
        }
    }
}
