using GPTipsBot.Dtos;
using GPTipsBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Repositories
{
    public interface IUserRepository
    {
        public long CreateUser(CreateEditUser user);
        long SoftlyRemoveUser(long telegramId);
        IEnumerable<User> GetAll();
    }
}
