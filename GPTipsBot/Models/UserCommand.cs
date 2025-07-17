using System.ComponentModel.DataAnnotations.Schema;

namespace GPTipsBot.Models;

public class UserCommand: Entity
{
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public CommandType Type { get; set; }
    // public string Text { get; set; }
}

public enum CommandType
{
    Start,
    ChooseLanguage,
    Image,
    TextRecognition,
    ResetContext,
    Help,
    SetRuLang,
    SetEngLang,
    StopRequest,
    CancelPreviousCommand,
    Admin,
    Deposit,
    GetProfile,
    Donate
}

public class Wallet: Entity
{
    [ForeignKey("User")]
    public long UserId { get; set; }
    public User User { get; set; }
    public double Balance { get; set; }
    public string Currency { get; set; }
}

public class Transaction: Entity
{
    public long WalletId { get; set; }
    public long Amount { get; set; }
}