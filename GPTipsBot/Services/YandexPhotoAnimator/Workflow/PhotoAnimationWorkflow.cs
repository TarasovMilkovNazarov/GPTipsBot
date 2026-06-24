using GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;
using WorkflowCore.Interface;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class PhotoAnimationWorkflow : IWorkflow<PhotoAnimationWorkflowData>
{
    public string Id => nameof(PhotoAnimationWorkflow);

    public int Version => 1;

    public void Build(IWorkflowBuilder<PhotoAnimationWorkflowData> builder)
    {
        builder
            .StartWith<DownloadPhotoStep>()
            .Then<UploadImageStep>()
            .Then<GenerateVideoStep>()
            .Then<WaitForVideoStep>()
                .Input(step => step.GenerationId, data => data.GenerationId!)
                .Input(step => step.ChatId, data => data.ChatId)
                .Input(step => step.ProgressMessageId, data => data.ProgressMessageId)
                .Output(data => data.VideoUrl, step => step.VideoUrl)
                .Output(data => data.ErrorMessage, step => step.ErrorMessage)
            .Then<NotifyUserStep>();
    }
}
