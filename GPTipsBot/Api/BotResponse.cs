﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Api
{
    public static class BotResponse
    {
        public static string TooManyRequests => "Слишком много запросов, попробуйте через 1 минуту";

        public static string Greeting => "Привет! Чем могу помочь?";

        public static string SomethingWentWrong = "Что-то пошло не так, попробуйте ещё раз";
        public static string OnMaintenance = "Ведутся технические работы. Благодарим за понимание.";
        public static string Typing = "Подождите, подготавливаю ответ...";

    }
}