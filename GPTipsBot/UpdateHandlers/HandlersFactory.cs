using Microsoft.Extensions.DependencyInjection;

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
