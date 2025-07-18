using System.ClientModel;
using System.ClientModel.Primitives;
using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using Moq;
using OpenAI.Chat;

namespace GPTipsBotTests.Services
{
    public static class GptApiMock
    {
        public static Mock<IGpt> CreateGptMock()
        {
            var mock = new Mock<IGpt>();
            var response = new SystemChatMessage("test");
            Mock<ClientResult<ChatCompletion>> mockResult = new(null, Mock.Of<PipelineResponse>());
            mockResult
                .SetupGet(result => result.Value)
                .Returns(ChatCompletion);

            mock.Setup(m => m.SendMessage(It.IsAny<UpdateDecorator>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResult.Object);

            return mock;
        }
    }
}