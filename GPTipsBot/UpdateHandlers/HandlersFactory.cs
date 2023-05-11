using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPTipsBot.UpdateHandlers
{
    public class MessageHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public MessageHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public BaseMessageHandler Create<T>() where T : BaseMessageHandler
        {
            return _serviceProvider.GetRequiredService(typeof(T)) as T;
        }
    }
}
