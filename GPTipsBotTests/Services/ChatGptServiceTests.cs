using NUnit.Framework;

namespace GPTipsBot.Services.Tests
{
    public class ChatGptServiceTests
    {
        [Test]
        public void OneSentence_CountTokens()
        {
            var deviation = 0.20d;

            (string text, int count) ethalonTextToTokensCount = ("Today, I went for a walk in the park and enjoyed the beautiful weather. The sun was shining, birds were chirping, and flowers were blooming. It was a perfect day to be outdoors and appreciate nature's wonders.", 50);

            Assert.LessOrEqual(Math.Abs(ethalonTextToTokensCount.count - ChatGptService.CountTokens(ethalonTextToTokensCount.text)), ethalonTextToTokensCount.count*deviation);
        }

        [Test]
        public void LongText_CountTokens()
        {
            var deviation = 0.20d;

            (string text, int count) ethalonTextToTokensCount = ("Искусственный интеллект (ИИ) - это область компьютерных наук, " +
                "которая занимается созданием интеллектуальных систем, способных имитировать человеческое мышление и выполнение сложных задач. " +
                "ИИ использует методы и алгоритмы, основанные на обработке больших объемов данных и обучении на основе опыта.\r\n\r\n" +
                "Главная цель искусственного интеллекта заключается в создании автономных систем, способных самостоятельно обучаться, принимать решения и решать проблемы." +
                " Это может быть достигнуто через различные подходы, такие как символьное вычисление, нейронные сети и генетические алгоритмы.\r\n\r\n" +
                "Одной из важных областей применения ИИ является медицина. Искусственный интеллект может помочь в диагностике заболеваний, разработке новых лекарственных " +
                "препаратов и оптимизации лечения. Также ИИ применяется в автономных транспортных системах, финансовых рынках, робототехнике и многих других сферах.\r\n\r\n" +
                "В целом, искусственный интеллект представляет собой перспективное направление развития, которое может существенно изменить нашу жизнь и улучшить многие аспекты общества.", 380);

            Assert.LessOrEqual(Math.Abs(ethalonTextToTokensCount.count - ChatGptService.CountTokens(ethalonTextToTokensCount.text)), ethalonTextToTokensCount.count*deviation);
        }
    }
}