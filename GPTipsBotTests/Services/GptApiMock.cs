using GPTipsBot.Dtos;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Moq;
using OpenAI.ObjectModels.ResponseModels;

namespace GPTipsBotTests.Services
{
    public static class GptApiMock
    {
        public static Mock<IGpt> CreateGptMock()
        {
            var mock = new Mock<IGpt>();
            var response = new ChatCompletionCreateResponse
            {
                Choices = new() { new(){ Message = new("system", "test") } }
            };

            mock.Setup(m => m.SendMessage(It.IsAny<UpdateDecorator>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            return mock;
        }
    }
}