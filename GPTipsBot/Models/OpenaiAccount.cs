namespace GPTipsBot.Models
{
    public class OpenaiAccount:Entity
    {
        public string Token { get; set; }
        public string OrganizationId { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsFreezed { get; set; }
        public double Balance { get; set; }
    }
}
