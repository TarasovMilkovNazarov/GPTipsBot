using GPTipsBot.Db;
using GPTipsBot.Dtos;
using GPTipsBot.Models;
using GPTipsBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories
{
    public class MessageRepository(ILogger<MessageRepository> logger, ApplicationContext context)
    {
        private readonly ILogger<MessageRepository> _logger = logger;

        public async Task<Message> AddAsync(MessageDto messageDto, Message? replyTo = null, long? forceContextId = null)
        {
            var contextId = forceContextId ?? (messageDto is { ContextBound: true, NewContext: false } ?
                GetLastContext(messageDto.UserId, messageDto.ChatId) : null);

            var newMessage = new Message()
            {
                Text = messageDto.Text,
                ChatId = messageDto.ChatId,
                UserId = messageDto.UserId,
                Role = messageDto.Role,
                ContextId = contextId,
                ContextBound = messageDto.ContextBound,
                TelegramMessageId = messageDto.TelegramMessageId,
                MessageThreadId = messageDto.MessageThreadId,
                ReplyTo = replyTo,
                CreatedAt = DateTime.UtcNow,
                Type = messageDto.BotMessageType
            };

            context.Messages.Add(newMessage);

            await context.SaveChangesAsync();

            messageDto.Id = newMessage.Id;
            messageDto.ContextId = newMessage.ContextId;

            return newMessage;
        }

        public IEnumerable<Message> GetAllUserMessages(long telegramId)
        {
            return context.Messages.AsNoTracking().Where(x => x.UserId == telegramId);
        }

        public long? GetLastContext(long userId, long chatId)
        {
            var lastMes = context.Messages.AsNoTracking().OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault(x => x.UserId == userId && x.ChatId == chatId && x.ContextId != null);

            return lastMes?.ContextId;
        }
        
        public List<Message> GetRecentContextMessages(UserChatKey userKey, long contextId)
        {
            var messages = context.Messages.AsNoTracking().Where(x =>
                    x.UserId == userKey.Id && 
                    x.ChatId == userKey.ChatId && 
                    x.ContextId == contextId)
                .Where(m => m.ContextBound)
                .OrderByDescending(x => x.CreatedAt).Take(ContextWindow.WindowSize);

            return messages.ToList();
        }

        public int GetTodayImagesCount(UserChatKey userKey)
        {
            var imagesCount = context.Messages.AsNoTracking()
                .Where(x => x.UserId == userKey.Id && x.Type == BotMessageType.ImageGenerated)
                .Count(m => m.CreatedAt.Date == DateTime.UtcNow.Date)
                ;

            return imagesCount;
        }

        public int GetTodayTextRecognitionCount(UserChatKey userKey)
        {
            var imagesCount = context.Messages.AsNoTracking()
                .Where(x => x.Type == BotMessageType.RecognizeText)
                .Count(m => m.CreatedAt.Date == DateTime.UtcNow.Date);

            return imagesCount;
        }

        public List<Message> GetChatMessagesForDay(
            long chatId,
            DateTime utcDay,
            long? messageThreadId = null,
            int limit = 200)
        {
            var dayStart = utcDay.Date;
            var dayEnd = dayStart.AddDays(1);

            return context.Messages.AsNoTracking()
                .Where(m => m.ChatId == chatId
                            && m.MessageThreadId == messageThreadId
                            && m.CreatedAt >= dayStart
                            && m.CreatedAt < dayEnd
                            && m.Text != null
                            && m.Text != "")
                .OrderBy(m => m.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public List<ConversationListItem> ListConversations(long userId, long chatId, int limit = 50)
        {
            var messages = context.Messages.AsNoTracking()
                .Where(m => m.UserId == userId
                            && m.ChatId == chatId
                            && m.ContextId != null
                            && m.ContextBound)
                .OrderByDescending(m => m.CreatedAt)
                .Take(500)
                .ToList();

            var metas = context.ConversationMetas.AsNoTracking()
                .Where(m => m.UserId == userId)
                .ToDictionary(m => m.ContextId);

            return messages
                .GroupBy(m => m.ContextId!.Value)
                .Select(g =>
                {
                    metas.TryGetValue(g.Key, out var meta);
                    if (meta?.IsDeleted == true)
                    {
                        return null;
                    }

                    var firstUser = g
                        .Where(x => x.Role == Enums.MessageOwner.User && !string.IsNullOrWhiteSpace(x.Text))
                        .OrderBy(x => x.CreatedAt)
                        .FirstOrDefault();
                    var title = !string.IsNullOrWhiteSpace(meta?.CustomTitle)
                        ? meta!.CustomTitle!.Trim()
                        : TruncateTitle(firstUser?.Text);
                    return new ConversationListItem(
                        g.Key,
                        title,
                        g.Max(x => x.CreatedAt),
                        meta?.IsPinned == true);
                })
                .Where(x => x is not null)
                .Cast<ConversationListItem>()
                .OrderByDescending(x => x.IsPinned)
                .ThenByDescending(x => x.UpdatedAt)
                .Take(limit)
                .ToList();
        }

        public List<Message> GetConversationMessages(long userId, long chatId, long contextId)
        {
            if (IsConversationDeleted(userId, contextId))
            {
                return [];
            }

            return context.Messages.AsNoTracking()
                .Where(m => m.UserId == userId
                            && m.ChatId == chatId
                            && m.ContextId == contextId
                            && m.ContextBound)
                .OrderBy(m => m.CreatedAt)
                .ToList();
        }

        public async Task<ConversationListItem?> RenameConversationAsync(long userId, long contextId, string title)
        {
            if (!OwnsConversation(userId, contextId) || IsConversationDeleted(userId, contextId))
            {
                return null;
            }

            var trimmed = title.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            if (trimmed.Length > 80)
            {
                trimmed = trimmed[..80];
            }

            var meta = await GetOrCreateMetaAsync(userId, contextId);
            meta.CustomTitle = trimmed;
            await context.SaveChangesAsync();

            return BuildListItem(userId, contextId, meta);
        }

        public async Task<ConversationListItem?> SetPinnedAsync(long userId, long contextId, bool pinned)
        {
            if (!OwnsConversation(userId, contextId) || IsConversationDeleted(userId, contextId))
            {
                return null;
            }

            var meta = await GetOrCreateMetaAsync(userId, contextId);
            meta.IsPinned = pinned;
            await context.SaveChangesAsync();

            return BuildListItem(userId, contextId, meta);
        }

        public async Task<bool> DeleteConversationAsync(long userId, long contextId)
        {
            if (!OwnsConversation(userId, contextId))
            {
                return false;
            }

            var meta = await GetOrCreateMetaAsync(userId, contextId);
            meta.IsDeleted = true;
            meta.IsPinned = false;
            await context.SaveChangesAsync();
            return true;
        }

        private bool OwnsConversation(long userId, long contextId) =>
            context.Messages.AsNoTracking().Any(m =>
                m.UserId == userId && m.ContextId == contextId && m.ContextBound);

        private bool IsConversationDeleted(long userId, long contextId) =>
            context.ConversationMetas.AsNoTracking()
                .Any(m => m.UserId == userId && m.ContextId == contextId && m.IsDeleted);

        private async Task<ConversationMeta> GetOrCreateMetaAsync(long userId, long contextId)
        {
            var meta = await context.ConversationMetas
                .FirstOrDefaultAsync(m => m.UserId == userId && m.ContextId == contextId);
            if (meta is not null)
            {
                return meta;
            }

            meta = new ConversationMeta { UserId = userId, ContextId = contextId };
            context.ConversationMetas.Add(meta);
            return meta;
        }

        private ConversationListItem BuildListItem(long userId, long contextId, ConversationMeta meta)
        {
            var messages = context.Messages.AsNoTracking()
                .Where(m => m.UserId == userId && m.ContextId == contextId && m.ContextBound)
                .ToList();
            var firstUser = messages
                .Where(x => x.Role == Enums.MessageOwner.User && !string.IsNullOrWhiteSpace(x.Text))
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefault();
            var title = !string.IsNullOrWhiteSpace(meta.CustomTitle)
                ? meta.CustomTitle!.Trim()
                : TruncateTitle(firstUser?.Text);
            var updatedAt = messages.Count == 0 ? DateTime.UtcNow : messages.Max(x => x.CreatedAt);
            return new ConversationListItem(contextId, title, updatedAt, meta.IsPinned);
        }

        private static string TruncateTitle(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "New chat";
            }

            text = text.Trim();
            return text.Length <= 60 ? text : text[..57] + "...";
        }
    }

    public sealed record ConversationListItem(long ContextId, string Title, DateTime UpdatedAt, bool IsPinned = false);
}
