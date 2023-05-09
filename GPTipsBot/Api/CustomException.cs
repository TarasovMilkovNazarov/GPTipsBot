using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.Api
{
    public class CustomException : Exception
    {
        public CustomException(string error) : base(error)
        {
            
        }
    }
}
