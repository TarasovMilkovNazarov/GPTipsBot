using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Extensions;
using GPTipsBot.Localization;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexCloud.Workflow.Steps;

public class NotifyUserStep(
    ITelegramBotClient botClient,
    IServiceScopeFactory scopeFactory,
    ILogger<NotifyUserStep> logger) : StepBodyAsync
{
    private const string DevPlaceholderUrl =
        "https://www.kasandbox.org/programming-images/avatars/leaf-blue.png";

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (ImageGenerationWorkflowData)context.Workflow.Data;
        using var _ = UiCultureScope.ForLanguage(data.UiLanguage);
        var success = false;

        try
        {
            if (data.UseDevPlaceholder)
            {
                await SendSuccessAsync(data, InputFile.FromUri(DevPlaceholderUrl));
                success = true;
            }
            else if (!string.IsNullOrEmpty(data.ImageBase64))
            {
                await using var imageStream = new MemoryStream(Convert.FromBase64String(data.ImageBase64));
                await SendSuccessAsync(data, InputFile.FromStream(imageStream, "image.png"));
                success = true;
            }
            else
            {
                await SendFailureAsync(data, BotResponse.SomethingWentWrong);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify user about image generation result for chat {ChatId}", data.ChatId);
            success = false;

            try
            {
                await SendFailureAsync(data, BotResponse.SomethingWentWrong);
            }
            catch (Exception notifyEx)
            {
                logger.LogError(notifyEx, "Failed to send image generation error message for chat {ChatId}", data.ChatId);
            }
        }
        finally
        {
            await FinalizePaymentAsync(data.PaymentHoldId, success);

            if (data.ProgressMessageId.HasValue && !data.IsInlineDelivery)
            {
                try
                {
                    await botClient.DeleteMessage(data.DeliveryChatId, data.ProgressMessageId.Value);
                }
                catch (Exception deleteEx)
                {
                    logger.LogDebug(deleteEx, "Failed to delete progress message for chat {ChatId}", data.ChatId);
                }
            }
        }

        return ExecutionResult.Next();
    }

    private async Task SendSuccessAsync(ImageGenerationWorkflowData data, InputFile photo)
    {
        if (data.IsInlineDelivery)
        {
            await botClient.EditMessageMedia(
                data.InlineMessageId!,
                new InputMediaPhoto(photo)
                {
                    Caption = TruncateCaption(data.Prompt),
                },
                replyMarkup: null);
        }
        else
        {
            await botClient.SendPhoto(data.DeliveryChatId, photo,
                replyMarkup: TelegramBotUiService.MenuIfPrivate(data.DeliveryChatId));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var messageRepository = scope.ServiceProvider.GetRequiredService<MessageRepository>();
        await messageRepository.AddAsync(new MessageDto(new UserChatKey(data.UserId, data.ChatId))
        {
            TelegramId = data.UserId,
            Role = MessageOwner.Ya,
            BotMessageType = BotMessageType.ImageGenerated,
        });

        if (data.IsInlineDelivery)
        {
            return;
        }

        await botClient.SendMessageWithMenuAsync(
            data.DeliveryChatId,
            string.Format(BotResponse.InputImageDescriptionText, ImageGeneratorHandler.ImageTextDescriptionLimit),
            TelegramBotUiService.GetImageInstructionInlineKeyboard(data.IsSquare),
            isGroupOrChannel: data.DeliveryChatId < 0);
    }

    private async Task SendFailureAsync(ImageGenerationWorkflowData data, string text)
    {
        if (data.IsInlineDelivery)
        {
            await botClient.EditMessageText(data.InlineMessageId!, text);
            return;
        }

        await botClient.SendMessageWithMenuAsync(data.DeliveryChatId, text);
    }

    private async Task FinalizePaymentAsync(long? paymentHoldId, bool success)
    {
        if (paymentHoldId is null)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        if (success)
        {
            await userService.ConfirmAsync(paymentHoldId.Value);
        }
        else
        {
            await userService.ReleaseAsync(paymentHoldId.Value);
        }
    }

    private static string TruncateCaption(string prompt)
    {
        const int limit = 1024;
        if (string.IsNullOrEmpty(prompt) || prompt.Length <= limit)
        {
            return prompt;
        }

        return prompt[..(limit - 1)] + "…";
    }
}
