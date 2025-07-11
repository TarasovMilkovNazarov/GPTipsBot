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
    Admin
}

public class Wallet: Entity
{
    public long Amount { get; set; }
    public string Currency { get; set; }
    public DateTime DepositDate { get; set; }
}

public class FinancialTransaction: Entity
{
    public long WalletId { get; set; }
    public long Amount { get; set; }
}