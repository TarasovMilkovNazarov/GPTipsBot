using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using OpenAI.ObjectModels.ResponseModels;

namespace GPTipsBotTests.Services
{
    internal class GptApiMock : IGpt
    {
        public Task<ChatCompletionCreateResponse> SendMessage(UpdateDecorator update, CancellationToken token)
        {
            var response = new ChatCompletionCreateResponse();
            response.Choices = new() { new(){ Message = new("system", "test") } };

            return Task.FromResult(new ChatCompletionCreateResponse());
        }
    }
}