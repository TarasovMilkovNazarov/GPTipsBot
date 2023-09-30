using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace GPTipsBot.Extensions
{
    public static class TelegramExtensions
    {
        public static string Serialize(this Update update)
        {
            return JsonConvert.SerializeObject(update, Formatting.Indented);
        } 
    }
}
