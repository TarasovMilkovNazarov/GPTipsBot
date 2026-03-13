using GPTipsBot.Dtos;
using GPTipsBot.Models;
using GPTipsBot.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Telegram.Bot.Types;

namespace GPTipsBot.Services;

[JsonObject(MemberSerialization.OptIn, NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public class BotMenu
{
    public const string StartCommand = "/start";
    public const string ImageCommand = "/image";
    public const string ImageSquareCommand = "/square_image";
    public const string ImageRectangleCommand = "/rect_image";
    public const string ImageCartoonifyCommand = "/cartoonify";
    public const string AnimatePhotoCommand = "/animate";
    public const string ResetContextCommand = "/reset_context";
    public const string HelpCommand = "/help";
    public const string ChooseLangCommand = "/setLang";
    public const string SetRuLangCommand = "/setRuLang";
    public const string SetEngLangCommand = "/setEngLang";
    public const string CancelCommand = "/cancel";
    public const string StopRequestCommand = "/stopRequest";
    public const string ImageTextRecognizeCommand = "/get_image_text";
    public const string DepositCommand = "/deposit";
    public const string GetProfileCommand = "/profile";
    public const string DonateCommand = "/donate";
    public const string MusicCommand = "/music";
    public const string SongCommand = "/song";
    public const string VideoCommand = "/video";

    public const string FixCommand = "/fix";
    public const string VersionCommand = "/version";

    public static CustomBotCommand Start => new() { Command = StartCommand, Description = BotUI.Start, Type = CommandType.Start};
    public static CustomBotCommand Image => new() { Command = ImageCommand, Description = BotUI.Image, Type = CommandType.Image };
    public static CustomBotCommand ImageCartoonify => new() { Command = ImageCartoonifyCommand, Description = BotUI.CartoonifyButton, Type = CommandType.ImageCartoonify };
    public static CustomBotCommand AnimatePhoto => new() { Command = AnimatePhotoCommand, Description = BotUI.AnimateButton, Type = CommandType.AnimatePhoto };
    public static CustomBotCommand ImageRectangle => new() { Command = ImageRectangleCommand, Type = CommandType.ImageRectangle };
    public static CustomBotCommand ImageSquare => new() { Command = ImageSquareCommand, Type = CommandType.ImageSquare };
    public static CustomBotCommand ImageRecText => new() { Command = ImageTextRecognizeCommand, Description = BotUI.ImageTextRecognize, Type = CommandType.TextRecognition};
    public static CustomBotCommand ResetContext => new() { Command = ResetContextCommand, Description = BotUI.ResetContext, Type = CommandType.ResetContext};
    public static CustomBotCommand Help => new() { Command = HelpCommand, Description = BotUI.Help, Type = CommandType.Help};
    public static CustomBotCommand ChooseLang => new() { Command = ChooseLangCommand, Description = BotUI.SetLang, Type = CommandType.ChooseLanguage};
    public static CustomBotCommand SetRuLang => new() { Command = SetRuLangCommand, Description = BotUI.SetRuLang, Type = CommandType.SetRuLang };
    public static CustomBotCommand SetEngLang => new() { Command = SetEngLangCommand, Description = BotUI.SetEngLang, Type = CommandType.SetEngLang };
    public static CustomBotCommand StopRequest => new() { Command = StopRequestCommand, Type = CommandType.StopRequest };
    public static CustomBotCommand Cancel => new() { Command = CancelCommand, Type = CommandType.CancelPreviousCommand };
    public static CustomBotCommand Deposit => new() { Command = DepositCommand, Description = BotUI.DepositButton, Type = CommandType.Deposit };
    public static CustomBotCommand Profile => new() { Command = GetProfileCommand, Description = BotUI.ProfileButton, Type = CommandType.GetProfile };
    public static CustomBotCommand Donate => new() { Command = DonateCommand, Description = BotUI.DonateButton, Type = CommandType.Donate };
    public static CustomBotCommand Music => new() { Command = MusicCommand, Description = BotUI.MusicButton, Type = CommandType.Music };
    public static CustomBotCommand Song => new() { Command = SongCommand, Description = BotUI.SongButton, Type = CommandType.Song, IsDisabled = true };
    public static CustomBotCommand Video => new() { Command = VideoCommand, Description = BotUI.VideoButton, Type = CommandType.Video };

    public static CustomBotCommand Fix => new() { Command = FixCommand, Type = CommandType.Admin };
    public static CustomBotCommand Version => new() { Command = VersionCommand, Type = CommandType.Admin };

    public BotMenu()
    {
    }

    public BotCommand[] GetBotCommands()
    {
        return new[]
        {
            Start,
            Image,
            ImageRecText,
            AnimatePhoto,
            ResetContext,
            Help,
            Deposit,
            Profile,
            Donate,
            Music,
            // Song,
            Video
        };
    }
}