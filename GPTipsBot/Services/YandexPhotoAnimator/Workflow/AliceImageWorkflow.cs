using GPTipsBot.Services.YandexPhotoAnimator.Workflow.Steps;
using WorkflowCore.Interface;

namespace GPTipsBot.Services.YandexPhotoAnimator.Workflow;

public class AliceImageWorkflow : IWorkflow<AliceImageWorkflowData>
{
    public string Id => nameof(AliceImageWorkflow);

    public int Version => 1;

    public void Build(IWorkflowBuilder<AliceImageWorkflowData> builder)
    {
        builder
            .StartWith<DownloadAliceImagesStep>()
            .Then<UploadAliceImagesStep>()
            .Then<GenerateAliceImageStep>()
            .Then<WaitForAliceImageStep>()
                .Input(step => step.GenerationId, data => data.GenerationId!)
                .Input(step => step.Kind, data => data.Kind)
                .Input(step => step.ChatId, data => data.ChatId)
                .Input(step => step.ProgressMessageId, data => data.ProgressMessageId)
                .Output(data => data.ResultImageUrl, step => step.ResultImageUrl)
                .Output(data => data.ErrorMessage, step => step.ErrorMessage)
            .Then<NotifyAliceImageStep>();
    }
}
