namespace GPTipsBot.Models;

public enum AuthProvider
{
    Guest = 0,
    Telegram = 1,
    Email = 2,
    Vkid = 3,
}

public class AuthLoginEvent
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public AuthProvider Provider { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
