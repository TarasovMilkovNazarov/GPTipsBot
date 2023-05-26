using GPTipsBot.Services;
using GPTipsBot.UpdateHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Api
{
    public static class BotResponse
    {
        public static string TooManyRequests => $"❗️Слишком много запросов, попробуйте через {MessageService.ResettingInterval.TotalSeconds} секунд";
        public static string TokensLimitExceeded => "Превышен лимит на количество символов. Попробуйте сократить ваш запрос";
        public static string ContextUpdated => "Контекст успешно сброшен. Начните новый диалог";
        public static string SendFeedback => "Введите сообщение ниже. Будем рад любой обратной связи.";
        public static string Cancel => "Операция отменена";
        public static string Thanks => "Ваш отзыв отправлен автору. Спасибо, что делаете бота лучше!";

        public static string Greeting => "Привет! Чем могу помочь?";

        public static string SomethingWentWrong = " Что-то пошло не так, попробуйте ещё раз";
        public static string SomethingWentWrongWithImageService = "Сервис генерации изображений находится в тестовом режиме. " +
            "Попробуйте заново, начиная с команды /image, либо дождитесь исправления ошибок. " +
            "Мы работаем над улучшением сервиса, благодарим за понимание 🙏";
        public static string ImageDescriptionLimitWarning = $"Текстовое описание изображения должно быть не более " +
            $"{ImageGeneratorToUserHandler.basedOnExperienceInputLengthLimit} символов. " +
            "Сократите описание и начните заново с команды /image";
        public static string OnMaintenance = "⚙️ Ведутся технические работы. Благодарим за понимание.";
        public static string Recovered = "Простите, я отутствовал какое-то время, но сейчас снова в строю.";
        public static string SwitchProxy => String.Format("Использовать бесплатное прокси: {0}", AppConfig.UseFreeApi);
        public static string PleaseWaitMsg = "⌛️ Подождите, подготавливаю ответ...";
        public static string OnlyMessagesAvailable = "Извините, но сейчас доступна обработка только текстовых сообщений. " +
            "В разработке находятся голосовые сообщения и генерация картинок текстом";

        public static string InputImageDescriptionText => $"Введите текстовое описание изображения длиной до " +
            $"{ImageGeneratorToUserHandler.basedOnExperienceInputLengthLimit} символов";
    }
}
