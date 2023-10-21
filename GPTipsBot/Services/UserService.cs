using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Telegram.Bot;

namespace GPTipsBot.Services
{
    public class UserService
    {
        private readonly ITelegramBotClient botClient;
        private readonly UserRepository userRepository;
        public event EventHandler<User> UserCreated;
        public static long? activeUserCount;

        public UserService(ITelegramBotClient botClient, UserRepository userRepository)
        {
            this.botClient = botClient;
            this.userRepository = userRepository;
            UserCreated += UserCreatedEventHandler;
        }

        public void CreateUpdateUser(User user)
        {
            var isExists = userRepository.Any(user.Id);
            if (isExists)
            {
                userRepository.Update(user);
                return;
            }

            userRepository.Create(user);
            UserCreated?.Invoke(this, user);
        }

        public void UserCreatedEventHandler(object sender, User user)
        {
            var fullName = user.FirstName;
            fullName += user.LastName == null ? "" : $" {user.LastName}";

            if (activeUserCount == null)
            {
                activeUserCount = userRepository.GetActiveUsersCount();
            }
            else
            {
                activeUserCount++;
            }

            var message = "#newUser" + Environment.NewLine + $"{fullName} with telegramId={user.Id} created";
            message += Environment.NewLine + $"Total count: {activeUserCount}";

            foreach (var adminId in AppConfig.AdminIds)
            {
                botClient.SendTextMessageAsync(adminId, message);   
            }
        }
    }
}
