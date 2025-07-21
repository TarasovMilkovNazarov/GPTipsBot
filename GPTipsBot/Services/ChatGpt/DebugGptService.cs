using GPTipsBot.UpdateHandlers;
using OpenAI.ObjectModels.ResponseModels;

namespace GPTipsBot.Services;

public class DebugGptService: IGpt
{
    public Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public Task<Stream> GenerateMusicByText(string text, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<Uri> GenerateSongByText(string text, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}