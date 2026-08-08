namespace GPTipsBot.Models
{
    public class BotSettings
    {
        public long Id { get; set; }
        public string Language { get; set; }
        /// <summary>OpenAI model id (e.g. gpt-5). Null means default (gpt-4o-mini).</summary>
        public string? PreferredGptModel { get; set; }
    }
}
