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
    public const string AnimatePhotoCommand = "/animate";
    public const string ResetContextCommand = "/reset_context";
    public const string HelpCommand = "/help";
    public const string ChooseLangCommand = "/setLang";
    public const string SetRuLangCommand = "/setRuLang";
    public const string SetEngLangCommand = "/setEngLang";
    public const string CancelCommand = "/cancel";
    public const string StopRequestCommand = "/stopRequest";
    public const string ImageTextRecognizeCommand = "/get_image_text";
    public const string PromptFromImageCommand = "/prompt_from_image";
    public const string DepositCommand = "/deposit";
    public const string GetProfileCommand = "/profile";
    public const string DonateCommand = "/donate";

    public const string ModelCommand = "/model";
    public const string ImagesMenuCommand = "/images";
    public const string GptImageCommand = "/gpt_image";
    public const string EditImageCommand = "/edit_image";
    public const string GptImageSizeSquareCommand = "/gpt_image_sq";
    public const string GptImageSizeLandscapeCommand = "/gpt_image_land";
    public const string GptImageSizePortraitCommand = "/gpt_image_port";
    public const string GptImageQualityLowCommand = "/gpt_image_q_low";
    public const string GptImageQualityMediumCommand = "/gpt_image_q_med";
    public const string GptImageQualityHighCommand = "/gpt_image_q_high";
    public const string SummaryCommand = "/summary";
    public const string AskCommand = "/ask";
    public const string FixCommand = "/fix";
    public const string VersionCommand = "/version";
    public const string VpnCheckCommand = "/vpn_check";

    public static CustomBotCommand Start => new() { Command = StartCommand, Description = BotUI.Start, Type = CommandType.Start};
    public static CustomBotCommand Image => new() { Command = ImageCommand, Description = BotUI.Image, Type = CommandType.Image };
    public static CustomBotCommand AnimatePhoto => new() { Command = AnimatePhotoCommand, Description = BotUI.AnimateButton, Type = CommandType.AnimatePhoto };
    public static CustomBotCommand ImageRectangle => new() { Command = ImageRectangleCommand, Type = CommandType.ImageRectangle };
    public static CustomBotCommand ImageSquare => new() { Command = ImageSquareCommand, Type = CommandType.ImageSquare };
    public static CustomBotCommand ImageRecText => new() { Command = ImageTextRecognizeCommand, Description = BotUI.ImageTextRecognize, Type = CommandType.TextRecognition};
    public static CustomBotCommand PromptFromImage => new() { Command = PromptFromImageCommand, Description = BotUI.PromptFromImage, Type = CommandType.PromptFromImage };
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
    public static CustomBotCommand Model => new() { Command = ModelCommand, Description = BotUI.Model, Type = CommandType.SelectModel };
    public static CustomBotCommand ImagesMenu => new() { Command = ImagesMenuCommand, Description = BotUI.ImagesMenu, Type = CommandType.ImagesMenu };
    public static CustomBotCommand GptImage => new() { Command = GptImageCommand, Description = BotUI.GptImage, Type = CommandType.GptImage };
    public static CustomBotCommand EditImage => new() { Command = EditImageCommand, Description = BotUI.EditImage, Type = CommandType.EditImage };
    public static CustomBotCommand GptImageSizeSquare => new() { Command = GptImageSizeSquareCommand, Type = CommandType.GptImage };
    public static CustomBotCommand GptImageSizeLandscape => new() { Command = GptImageSizeLandscapeCommand, Type = CommandType.GptImage };
    public static CustomBotCommand GptImageSizePortrait => new() { Command = GptImageSizePortraitCommand, Type = CommandType.GptImage };
    public static CustomBotCommand GptImageQualityLow => new() { Command = GptImageQualityLowCommand, Type = CommandType.GptImage };
    public static CustomBotCommand GptImageQualityMedium => new() { Command = GptImageQualityMediumCommand, Type = CommandType.GptImage };
    public static CustomBotCommand GptImageQualityHigh => new() { Command = GptImageQualityHighCommand, Type = CommandType.GptImage };
    public static CustomBotCommand Summary => new() { Command = SummaryCommand, Description = BotUI.Summary, Type = CommandType.Summary };
    public static CustomBotCommand Ask => new() { Command = AskCommand, Description = BotUI.Ask, Type = CommandType.Ask };

    public static CustomBotCommand Fix => new() { Command = FixCommand, Type = CommandType.Admin };
    public static CustomBotCommand Version => new() { Command = VersionCommand, Type = CommandType.Admin };

    public BotMenu()
    {
    }

    public BotCommand[] GetBotCommands()
    {
        return
        [
            Start,
            Image,
            ImageRecText,
            PromptFromImage,
            AnimatePhoto,
            ResetContext,
            Help,
            Deposit,
            Profile,
            Donate,
            Model,
            GptImage,
            EditImage,
            Summary,
            Ask
        ];
    }

    public BotCommand[] GetGroupBotCommands()
    {
        return
        [
            Ask,
            Image,
            Summary
        ];
    }

    public static bool IsAllowedInGroup(CommandType type) =>
        type is CommandType.Ask
            or CommandType.Image
            or CommandType.ImageSquare
            or CommandType.ImageRectangle
            or CommandType.Summary
            or CommandType.CancelPreviousCommand
            or CommandType.StopRequest;
}
