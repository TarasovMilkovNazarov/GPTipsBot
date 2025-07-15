using GPTipsBot.UpdateHandlers;
using OpenAI.ObjectModels.ResponseModels;

namespace GPTipsBot.Services;

public class DebugGptService: IGpt
{
    public Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
    {
        throw new NotImplementedException();
    }
}