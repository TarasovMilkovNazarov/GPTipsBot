using Discord;

namespace GPTipsBot.Dtos.FaceSwap
{
    public class Interaction
    {
        public int type { get; set; }
        public ulong application_id { get; set; }
        public ulong guild_id { get; set; }
        public ulong channel_id { get; set; }
        public string session_id { get; set; }
        public Data data { get; set; }
        public string nonce { get; set; }
    }
}