using GPTipsBot.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
