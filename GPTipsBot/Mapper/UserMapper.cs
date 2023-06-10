using GPTipsBot.Dtos;
using GPTipsBot.Models;

namespace GPTipsBot.Mapper
{
    public static class UserMapper
    {
        public static User MapToUser(TelegramGptMessageUpdate messageDto)
        {
            User user = new User()
            {
                Id = messageDto.UserKey.Id,
                FirstName = messageDto.FirstName,
                LastName = messageDto.LastName,
                Source = messageDto.Source,
                IsActive = messageDto.IsActive,
                CreatedAt = DateTime.UtcNow,
            };

            return user;
        }
    }
}
