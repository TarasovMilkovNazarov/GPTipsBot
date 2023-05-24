using GPTipsBot.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Dtos
{
    public class UserStateDto
    {
        public UserStateDto(UserKey userKey)
        {
            CurrentState = UserStateEnum.None;
            UserKey = userKey;
        }

        public UserKey UserKey { get; }
        public UserStateEnum CurrentState { get; set; }
        public Dictionary<long, CancellationTokenSource> messageIdToCancellation = new ();
    }
}
