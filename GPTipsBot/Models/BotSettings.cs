namespace GPTipsBot.Models
{
    public class BotSettings
    {
        public long Id { get; set; }
        public string Language { get; set; }
        /// <summary>OpenAI model id (e.g. gpt-5). Null means default (gpt-4o-mini).</summary>
        public string? PreferredGptModel { get; set; }
        /// <summary>
        /// <see cref="PaymentProvider"/> name (e.g. "LavaTop") the user last picked on /deposit. Null
        /// means they've never chosen one yet, so /deposit shows the method-choice screen first.
        /// </summary>
        public string? PreferredPaymentProvider { get; set; }
    }
}
