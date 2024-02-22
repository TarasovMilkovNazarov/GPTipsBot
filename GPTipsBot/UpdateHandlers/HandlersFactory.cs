using Microsoft.Extensions.DependencyInjection;

namespace GPTipsBot.UpdateHandlers
{
    public class HandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public HandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public BaseMessageHandler Create<T>() where T : BaseMessageHandler
        {
            return _serviceProvider.GetRequiredService(typeof(T)) as T ?? throw new InvalidOperationException();
        }
    }
}
