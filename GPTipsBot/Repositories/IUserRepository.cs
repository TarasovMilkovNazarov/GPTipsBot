using GPTipsBot.Dtos;
using GPTipsBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Repositories
{
    public interface IUserRepository : IDisposable
    {
        public long CreateUpdateUser(CreateEditUser user);
        long SoftlyRemoveUser(long telegramId);
        IEnumerable<User> GetAll();
    }
}
