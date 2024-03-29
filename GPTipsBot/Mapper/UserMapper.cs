﻿using GPTipsBot.Dtos;
using GPTipsBot.Models;

namespace GPTipsBot.Mapper
{
    public static class UserMapper
    {
        public static User Map(UserDto userDto)
        {
            User user = new User()
            {
                Id = userDto.Id,
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Source = userDto.Source,
                IsActive = userDto.IsActive,
                CreatedAt = DateTime.UtcNow,
            };

            return user;
        }

        public static UserDto Map(Telegram.Bot.Types.User? telegramUser)
        {
            UserDto user = new UserDto()
            {
                Id = telegramUser.Id,
                FirstName = telegramUser.FirstName,
                LastName = telegramUser.LastName,
                CreatedAt = DateTime.UtcNow,
            };

            return user;
        }
    }
}
