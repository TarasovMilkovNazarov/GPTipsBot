using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Localization;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;

public class NotifyUserStep(
    ITelegramBotClient botClient,
    IServiceScopeFactory scopeFactory,
    ILogger<NotifyUserStep> logger) : StepBodyAsync
{
    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (PhotoAnimationWorkflowData)context.Workflow.Data;
        using var _ = UiCultureScope.ForLanguage(data.UiLanguage);
        var success = false;

        try
        {
            if (!string.IsNullOrEmpty(data.VideoUrl))
            {
                await botClient.SendVideo(data.UserId, InputFile.FromUri(data.VideoUrl));

                await using var scope = scopeFactory.CreateAsyncScope();
                var messageRepository = scope.ServiceProvider.GetRequiredService<MessageRepository>();
                await messageRepository.AddAsync(new MessageDto(new UserChatKey(data.UserId, data.ChatId))
                {
                    TelegramId = data.UserId,
                    Role = MessageOwner.Ya,
                    BotMessageType = BotMessageType.AnimatedPhoto,
                });
                success = true;
            }
            else
            {
                await botClient.SendMessage(data.ChatId, BotResponse.PhotoAnimationFailed);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify user about photo animation result for chat {ChatId}", data.ChatId);
            success = false;

            try
            {
                await botClient.SendMessage(data.ChatId, BotResponse.SomethingWentWrong);
            }
            catch (Exception notifyEx)
            {
                logger.LogError(notifyEx, "Failed to send photo animation error message for chat {ChatId}", data.ChatId);
            }
        }
        finally
        {
            await FinalizePaymentAsync(data.PaymentHoldId, success);

            if (data.ProgressMessageId.HasValue)
            {
                try
                {
                    await botClient.DeleteMessage(data.ChatId, data.ProgressMessageId.Value);
                }
                catch (Exception deleteEx)
                {
                    logger.LogDebug(deleteEx, "Failed to delete progress message for chat {ChatId}", data.ChatId);
                }
            }
        }

        return ExecutionResult.Next();
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
}
