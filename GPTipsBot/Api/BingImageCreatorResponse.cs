using GPTipsBot.UpdateHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Api
{
    public static class BingImageCreatorResponse
    {
        public static string TimeoutError = "⚠️Ваш запрос превысил время ожидания.";
        public static string RedirectError = "⚠️Не удалось выполнить перенаправление.";
        public static string BlockedPromptError = "⚠️Ваш запрос был заблокирован. Попробуйте изменить некорректные слова и повторите попытку.";
        public static string NoResultsError = "Не удалось получить результаты.";
        public static string UnsupportedLangError = "Данный язык в настоящее время не поддерживается.";
        public static string BadImagesError = "Не удалось сгенерировать изображение. Попробуйте изменить запрос.";
        public static string NoImagesError = "Изображения не найдены.";
    }
}
