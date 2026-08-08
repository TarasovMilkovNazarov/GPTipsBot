using GPTipsBot.Dtos;
using GPTipsBot.Enums;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
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

        try
        {
            if (data.UseDevPlaceholder)
            {
                await SendSuccessAsync(data, InputFile.FromUri(DevPlaceholderUrl));
            }
            else if (!string.IsNullOrEmpty(data.ImageBase64))
            {
                await using var imageStream = new MemoryStream(Convert.FromBase64String(data.ImageBase64));
                await SendSuccessAsync(data, InputFile.FromStream(imageStream));
            }
            else
            {
                await botClient.SendMessage(data.DeliveryChatId, BotResponse.SomethingWentWrong);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify user about image generation result for chat {ChatId}", data.ChatId);

            try
            {
                await botClient.SendMessage(data.DeliveryChatId, BotResponse.SomethingWentWrong);
            }
            catch (Exception notifyEx)
            {
                logger.LogError(notifyEx, "Failed to send image generation error message for chat {ChatId}", data.ChatId);
            }
        }
        finally
        {
            if (data.ProgressMessageId.HasValue)
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
        await botClient.SendPhoto(data.DeliveryChatId, photo);

        await using var scope = scopeFactory.CreateAsyncScope();
        var messageRepository = scope.ServiceProvider.GetRequiredService<MessageRepository>();
        await messageRepository.AddAsync(new MessageDto(new UserChatKey(data.UserId, data.ChatId))
        {
            TelegramId = data.UserId,
            Role = MessageOwner.Ya,
            BotMessageType = BotMessageType.ImageGenerated,
        });

        await botClient.SendMessage(
            data.DeliveryChatId,
            string.Format(BotResponse.InputImageDescriptionText, ImageGeneratorHandler.ImageTextDescriptionLimit),
            replyMarkup: TelegramBotUiService.GetImageInstructionInlineKeyboard(data.IsSquare));
    }
}
