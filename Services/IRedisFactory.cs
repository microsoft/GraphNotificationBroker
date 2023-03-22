using StackExchange.Redis;

namespace GraphNotifications.Services
{
    public interface IRedisFactory : IDisposable
    {
        IDatabase GetCache();

        void ForceReconnect();
    }
}