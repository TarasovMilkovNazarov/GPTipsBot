﻿using Discord;

namespace GPTipsBot.Dtos.FaceSwap
{
    public class Option
    {
        public ApplicationCommandOptionType type { get; set; }
        public string name { get; set; }
        public string value { get; set; }
    }
}