using GPTipsBot.Dtos;

namespace GPTipsBot.Services
{
    public enum ChatGateResult
    {
        /// <summary>Process without occupying the per-chat slot (commands / group chatter).</summary>
        Bypass,

        /// <summary>Caller holds the slot and must Release when done.</summary>
        ProcessNow,

        /// <summary>Slot busy; this update is stored as the single pending item.</summary>
        Queued,

        /// <summary>Slot busy and pending already set; ignore silently.</summary>
        Dropped
    }

    /// <summary>
    /// Per-chat gate: at most one active update and one silently queued follow-up.
    /// </summary>
    public class RateLimiter
    {
        private readonly Dictionary<long, ChatGateState> _chats = new();
        private readonly object _sync = new();

        private sealed class ChatGateState
        {
            public bool Busy;
            public UpdateDecorator? Pending;
        }

        public ChatGateResult TryEnter(UpdateDecorator update)
        {
            // In groups, non-addressed chatter is only archived for /summary and must not
            // occupy the chat processing slot.
            if (update.IsGroupOrChannel && !update.IsAddressedToBot)
            {
                return ChatGateResult.Bypass;
            }

            if (update.IsCommand || update.IsInline)
            {
                return ChatGateResult.Bypass;
            }

            var chatId = update.UserChatKey.ChatId;

            lock (_sync)
            {
                if (!_chats.TryGetValue(chatId, out var state))
                {
                    state = new ChatGateState();
                    _chats[chatId] = state;
                }

                if (!state.Busy)
                {
                    state.Busy = true;
                    return ChatGateResult.ProcessNow;
                }

                if (state.Pending == null)
                {
                    state.Pending = update;
                    return ChatGateResult.Queued;
                }

                return ChatGateResult.Dropped;
            }
        }

        /// <summary>
        /// Ends the active slot. Returns the pending update (keeping busy) or null (slot freed).
        /// </summary>
        public UpdateDecorator? Release(long chatId)
        {
            lock (_sync)
            {
                if (!_chats.TryGetValue(chatId, out var state))
                {
                    return null;
                }

                if (state.Pending != null)
                {
                    var pending = state.Pending;
                    state.Pending = null;
                    return pending;
                }

                _chats.Remove(chatId);
                return null;
            }
        }

        /// <summary>Drops busy/pending for the chat (error recovery).</summary>
        public void ForceRelease(long chatId)
        {
            lock (_sync)
            {
                _chats.Remove(chatId);
            }
        }

        /// <summary>Clears all chat gates (tests).</summary>
        public void Reset()
        {
            lock (_sync)
            {
                _chats.Clear();
            }
        }
    }
}
