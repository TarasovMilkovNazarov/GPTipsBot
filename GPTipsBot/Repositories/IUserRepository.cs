using GPTipsBot.Dtos;
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
    }
}
