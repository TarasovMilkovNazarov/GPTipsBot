using GPTipsBot.Services;
using NUnit.Framework;

namespace GPTipsBotTests.Services
{
    internal class ImageCreatorTests
    {
        [Test]
        public void CreateImageTest()
        {
            var service = new ImageCreatorService();
            service.CreateImageFromText("three eyes cat");
        }
    }
}
