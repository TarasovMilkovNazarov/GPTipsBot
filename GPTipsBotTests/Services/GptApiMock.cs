using GPTipsBot.Dtos;
using GPTipsBot.Services;
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
            mock.Setup(m => m.SendOneOffAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(response);

            return mock;
        }
    }
}