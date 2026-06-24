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
            .Then<NotifyUserStep>();
    }
}
