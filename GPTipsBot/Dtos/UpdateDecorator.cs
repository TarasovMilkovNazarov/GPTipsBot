using System.Reflection;
using System.Text;
using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Exceptions;
using GPTipsBot.Extensions;
using GPTipsBot.Mapper;
using GPTipsBot.Mappers;
using GPTipsBot.Services;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;

namespace GPTipsBot.Dtos
{
    public class UpdateDecorator
    {
        public Update TelegramUpdate { get; }

        public UpdateDecorator(Update telegramUpdate)
        {
            TelegramUpdate = telegramUpdate;
            long chatId;

            switch (telegramUpdate.Type)
            {
                case UpdateType.PreCheckoutQuery:
                    Guard.Against.Null(telegramUpdate.PreCheckoutQuery);
                    UserChatKey = telegramUpdate.PreCheckoutQuery.From.Id;
                    User = UserMapper.Map(telegramUpdate.PreCheckoutQuery.From);
                    break;
                case UpdateType.Message when IsSupportedChat(telegramUpdate.Message?.Chat.Type):
                    Guard.Against.Null(telegramUpdate.Message);
                    Guard.Against.Null(telegramUpdate.Message.From);

                    chatId = telegramUpdate.Message.Chat.Id;

                    User = UserMapper.Map(telegramUpdate.Message.From);
                    User.Source = TelegramService.GetSource(telegramUpdate.Message.Text);
                    Message = MessageMapper.Map(telegramUpdate.Message, chatId, Enums.MessageOwner.User);
                    UserChatKey = new UserChatKey(telegramUpdate.Message.From.Id, chatId);

                    if (telegramUpdate.Message.Type == MessageType.Photo)
                    {
                        Guard.Against.Null(telegramUpdate.Message.Photo);
                        FileId = telegramUpdate.Message.Photo[^1].FileId;
                    }

                    Message.SuccessfulPayment = telegramUpdate.Message.SuccessfulPayment;
                    break;
                case UpdateType.CallbackQuery:
                    Guard.Against.Null(telegramUpdate.CallbackQuery);
                    Guard.Against.Null(telegramUpdate.CallbackQuery.Message);
                    Guard.Against.Null(telegramUpdate.CallbackQuery.Data);
                    chatId = telegramUpdate.CallbackQuery.Message.Chat.Id;
                    Message = MessageMapper.Map(telegramUpdate.CallbackQuery.Message, chatId, Enums.MessageOwner.User);
                    Message.UserId = telegramUpdate.CallbackQuery.From.Id;
                    User = UserMapper.Map(telegramUpdate.CallbackQuery.From);
                    UserChatKey = new UserChatKey(telegramUpdate.CallbackQuery.From.Id, chatId);
                    Message.Text = telegramUpdate.CallbackQuery.Data;
                    break;
                case UpdateType.MyChatMember:
                    Guard.Against.Null(telegramUpdate.MyChatMember);
                    var oldChatMemberStatus = telegramUpdate.MyChatMember.OldChatMember.Status;
                    var newChatMemberStatus = telegramUpdate.MyChatMember.NewChatMember.Status;
                    if (oldChatMemberStatus == ChatMemberStatus.Kicked &&
                        newChatMemberStatus == ChatMemberStatus.Member)
                    {
                        User = UserMapper.Map(telegramUpdate.MyChatMember.From);
                        IsRecovered = true;
                        UserChatKey = new UserChatKey(telegramUpdate.MyChatMember.From.Id, telegramUpdate.MyChatMember.Chat.Id);
                    }
                    break;
                default:
                    throw new IgnoreMessageTypeException(telegramUpdate.Type);
            }

            Language = telegramUpdate.GetLanguageOrDefault();

            var groupChatTypes = new ChatType?[] { ChatType.Supergroup, ChatType.Group, ChatType.Channel };
            IsGroupOrChannel = groupChatTypes.Contains(Message?.ChatType);

            TryGetCommand(Message?.Text, out var command);
            Command = command;

            if (IsGroupOrChannel && Message != null && !string.IsNullOrEmpty(Message.Text) && !IsCommand)
            {
                Message.Text = StripBotMentions(Message.Text, Message.Entities);
            }
        }

        public CustomBotCommand? Command { get; }

        public string? FileId { get; }

        public UserChatKey UserChatKey { get; }
        public UserDto User { get; }

        public MessageDto Message { get; }
        public bool IsRecovered { get; }
        public bool IsCommand => Command != null;
        public bool IsGroupOrChannel { get; }

        /// <summary>
        /// Chat where feature replies should be delivered (always the source chat).
        /// </summary>
        public long ReplyChatId => UserChatKey.ChatId;

        public CallbackQuery? CallbackQuery => TelegramUpdate.CallbackQuery;
        public PreCheckoutQuery? PreCheckoutQuery => TelegramUpdate.PreCheckoutQuery;

        public string Language { get; }

        /// <summary>
        /// In groups the bot should react only when explicitly addressed
        /// (command, @mention, or reply to the bot), unless a multi-step flow is active.
        /// </summary>
        public bool IsAddressedToBot
        {
            get
            {
                if (!IsGroupOrChannel)
                {
                    return true;
                }

                if (IsCommand || CallbackQuery != null || PreCheckoutQuery != null)
                {
                    return true;
                }

                if (Message?.SuccessfulPayment != null)
                {
                    return true;
                }

                return IsReplyToThisBot()
                       || MentionsThisBot(TelegramUpdate.Message?.Text, TelegramUpdate.Message?.Entities)
                       || MentionsThisBot(TelegramUpdate.Message?.Caption, TelegramUpdate.Message?.CaptionEntities);
            }
        }

        public override string ToString()
        {
            var serialized = JsonConvert.SerializeObject(TelegramUpdate, Formatting.Indented);

            return serialized;
        }

        public static bool TryGetCommandArgument(string? message, string command, out string argument)
        {
            argument = string.Empty;
            if (string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            var text = message.Trim();
            if (text.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase))
            {
                argument = text[(command.Length + 1)..].Trim();
                return true;
            }

            var botName = AppConfig.BotName;
            if (string.IsNullOrWhiteSpace(botName))
            {
                return false;
            }

            var prefix = $"{command}@{botName}";
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            argument = text[prefix.Length..].TrimStart();
            return !string.IsNullOrEmpty(argument);
        }

        private static bool IsSupportedChat(ChatType? chatType) =>
            chatType is ChatType.Private or ChatType.Group or ChatType.Supergroup;

        private bool IsReplyToThisBot()
        {
            var replyFrom = Message?.ReplyToMessage?.From;
            if (replyFrom is not { IsBot: true })
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(AppConfig.BotName))
            {
                return true;
            }

            return string.Equals(replyFrom.Username, AppConfig.BotName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MentionsThisBot(string? text, MessageEntity[]? entities)
        {
            if (string.IsNullOrEmpty(text) || entities == null || string.IsNullOrWhiteSpace(AppConfig.BotName))
            {
                return false;
            }

            var botMention = "@" + AppConfig.BotName;
            foreach (var entity in entities)
            {
                if (entity.Type == MessageEntityType.Mention)
                {
                    if (entity.Offset + entity.Length > text.Length)
                    {
                        continue;
                    }

                    var mention = text.Substring(entity.Offset, entity.Length);
                    if (string.Equals(mention, botMention, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                else if (entity.Type == MessageEntityType.TextMention
                         && entity.User is { IsBot: true }
                         && string.Equals(entity.User.Username, AppConfig.BotName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripBotMentions(string text, MessageEntity[]? entities)
        {
            if (entities == null || string.IsNullOrWhiteSpace(AppConfig.BotName))
            {
                return text.Trim();
            }

            var botMention = "@" + AppConfig.BotName;
            var removeRanges = new List<(int Offset, int Length)>();

            foreach (var entity in entities)
            {
                if (entity.Offset + entity.Length > text.Length)
                {
                    continue;
                }

                if (entity.Type == MessageEntityType.Mention)
                {
                    var mention = text.Substring(entity.Offset, entity.Length);
                    if (string.Equals(mention, botMention, StringComparison.OrdinalIgnoreCase))
                    {
                        removeRanges.Add((entity.Offset, entity.Length));
                    }
                }
                else if (entity.Type == MessageEntityType.TextMention
                         && entity.User is { IsBot: true }
                         && string.Equals(entity.User.Username, AppConfig.BotName, StringComparison.OrdinalIgnoreCase))
                {
                    removeRanges.Add((entity.Offset, entity.Length));
                }
            }

            if (removeRanges.Count == 0)
            {
                return text.Trim();
            }

            var sb = new StringBuilder(text);
            foreach (var (offset, length) in removeRanges.OrderByDescending(r => r.Offset))
            {
                sb.Remove(offset, length);
            }

            return string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool TryGetCommand(string? message, out CustomBotCommand? command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            message = message.Trim().ToLower();

            var classType = typeof(BotMenu);
            var properties = classType.GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(CustomBotCommand));

            foreach (var property in properties)
            {
                if (property.GetValue(null) is not CustomBotCommand botCommand) continue;

                var slashCommandValue = botCommand.Command;

                if (MatchesSlashCommand(message, slashCommandValue.ToLower()) ||
                    (TelegramBotUiService.ButtonToLocalizations.ContainsKey(slashCommandValue) &&
                     TelegramBotUiService.ButtonToLocalizations[slashCommandValue].Exists(b => b.ToLower() == message)))
                {
                    command = botCommand;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesSlashCommand(string message, string command)
        {
            if (message == command || message.StartsWith(command + " ", StringComparison.Ordinal))
            {
                return true;
            }

            var withAt = command + "@";
            if (!message.StartsWith(withAt, StringComparison.Ordinal))
            {
                return false;
            }

            var afterAt = message[withAt.Length..];
            var botName = AppConfig.BotName?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(botName))
            {
                // Bot name not loaded yet — accept any @suffix so local tests keep working.
                return true;
            }

            return afterAt == botName || afterAt.StartsWith(botName + " ", StringComparison.Ordinal);
        }
    }
}
