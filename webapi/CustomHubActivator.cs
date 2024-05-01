using Microsoft.AspNetCore.SignalR;

namespace webapi
{
    public class CustomHubActivator<THub> : IHubActivator<THub> where THub : Hub
    {
        private readonly IServiceProvider _serviceProvider;

        public CustomHubActivator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public THub Create()
        {
            return ActivatorUtilities.CreateInstance<THub>(_serviceProvider);
        }

        public void Release(THub hub)
        {
            // Optionally dispose your hub if needed.
        }
    }
}
