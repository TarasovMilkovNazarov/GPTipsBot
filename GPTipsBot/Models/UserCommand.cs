using System.ComponentModel.DataAnnotations.Schema;

namespace GPTipsBot.Models;

public class UserCommand: Entity
{
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public CommandType Type { get; set; }
}

public enum CommandType
{
    Start = 0,
    ChooseLanguage = 1,
    Image = 2,
    TextRecognition = 3,
    ResetContext = 4,
    Help = 5,
    SetRuLang = 6,
    SetEngLang = 7,
    StopRequest = 8,
    CancelPreviousCommand = 9,
    Admin = 10,
    Deposit = 11,
    GetProfile = 12,
    Donate = 13,
    Music = 14,
    Song = 15,
    Video = 16,
    ImageRectangle = 17,
    ImageSquare = 18,
    ImageCartoonify = 19,
    AnimatePhoto = 20,
    Summary = 21,
    Ask = 22,
    SelectModel = 23,
    GptImage = 24,
    EditImage = 25,
    ImagesMenu = 26,
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
    [ForeignKey("Wallet")]
    public long WalletId { get; set; }
    public long Amount { get; set; }
    public Wallet Wallet { get; set; }
}