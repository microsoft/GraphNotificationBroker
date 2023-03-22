

using GraphNotifications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Threading;

namespace GraphNotifications.Services
{
    /// <summary>
    /// Implements connection to Redis
    /// </summary> 
    public class RedisFactory : IRedisFactory
    {
        private static Lazy<IConnectionMultiplexer> _multiplexer;
        private static Lazy<IDatabase> _cache;
        private bool _disposed = false;

        private readonly AppSettings _settings;
        private readonly ILogger<RedisFactory> _logger;

        // Force Reconnect variables
        static long lastReconnectTicks = DateTimeOffset.MinValue.UtcTicks;
        static DateTimeOffset firstError = DateTimeOffset.MinValue;
        static DateTimeOffset previousError = DateTimeOffset.MinValue;

        static object reconnectLock = new object();

        // In general, let StackExchange.Redis handle most reconnects, 
        // so limit the frequency of how often this will actually reconnect.
        public static TimeSpan ReconnectMinFrequency = TimeSpan.FromSeconds(60);

        // if errors continue for longer than the below threshold, then the 
        // multiplexer seems to not be reconnecting, so re-create the multiplexer
        public static TimeSpan ReconnectErrorThreshold = TimeSpan.FromSeconds(30);

        public RedisFactory(IOptions<AppSettings> settings, ILogger<RedisFactory> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _multiplexer = CreateMultiplexer();
            _cache = GetDatabase();
        }

        public IDatabase GetCache()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RedisFactory));

            return _cache.Value;
        }

        private Lazy<IConnectionMultiplexer> CreateMultiplexer()
        {
            _logger.LogInformation("Connecting to Redis");
            return new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(_settings.RedisConnectionString));
        }

        private Lazy<IDatabase> GetDatabase()
        {
            return new Lazy<IDatabase>(_multiplexer.Value.GetDatabase());
        }

        private void CloseMultiplexer(Lazy<IConnectionMultiplexer> oldMultiplexer)
        {
            if (oldMultiplexer != null)
            {
                try
                {
                    oldMultiplexer.Value.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error closing old multiplexer. Message = {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Force a new ConnectionMultiplexer to be created.
        /// NOTES: 
        ///     1. Users of the ConnectionMultiplexer MUST handle ObjectDisposedExceptions, which can now happen as a result of calling ForceReconnect()
        ///     2. Don't call ForceReconnect for Timeouts, just for RedisConnectionExceptions or SocketExceptions
        ///     3. Call this method every time you see a connection exception, the code will wait to reconnect:
        ///         a. for at least the "ReconnectErrorThreshold" time of repeated errors before actually reconnecting
        ///         b. not reconnect more frequently than configured in "ReconnectMinFrequency"
        /// </summary>
        public void ForceReconnect()
        {
            _logger.LogInformation("Force Reconnect called");
            var utcNow = DateTimeOffset.UtcNow;
            var previousTicks = Interlocked.Read(ref lastReconnectTicks);
            var previousReconnect = new DateTimeOffset(previousTicks, TimeSpan.Zero);
            var elapsedSinceLastReconnect = utcNow - previousReconnect;

            // If mulitple threads call ForceReconnect at the same time, we only want to honor one of them.
            if (elapsedSinceLastReconnect > ReconnectMinFrequency)
            {
                lock (reconnectLock)
                {
                    utcNow = DateTimeOffset.UtcNow;
                    elapsedSinceLastReconnect = utcNow - previousReconnect;

                    if (firstError == DateTimeOffset.MinValue)
                    {
                        // We haven't seen an error since last reconnect, so set initial values.
                        firstError = utcNow;
                        previousError = utcNow;
                        return;
                    }

                    if (elapsedSinceLastReconnect < ReconnectMinFrequency)
                        return; // Some other thread made it through the check and the lock, so nothing to do.

                    var elapsedSinceFirstError = utcNow - firstError;
                    var elapsedSinceMostRecentError = utcNow - previousError;

                    var shouldReconnect =
                        elapsedSinceFirstError >= ReconnectErrorThreshold   // make sure we gave the multiplexer enough time to reconnect on its own if it can
                        && elapsedSinceMostRecentError <= ReconnectErrorThreshold; //make sure we aren't working on stale data (e.g. if there was a gap in errors, don't reconnect yet).

                    // Update the previousError timestamp to be now (e.g. this reconnect request)
                    previousError = utcNow;
                    _logger.LogInformation($"Should reconnect = ({shouldReconnect})");
                    if (shouldReconnect)
                    {
                        _logger.LogInformation($"Reconnecting to Redis");
                        firstError = DateTimeOffset.MinValue;
                        previousError = DateTimeOffset.MinValue;

                        var oldMultiplexer = _multiplexer;
                        CloseMultiplexer(oldMultiplexer);
                        _multiplexer = CreateMultiplexer();
                        _cache = GetDatabase();
                        Interlocked.Exchange(ref lastReconnectTicks, utcNow.UtcTicks);
                        _logger.LogInformation($"Reconnected to Redis");
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _multiplexer.IsValueCreated)
                {
                    _multiplexer.Value?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}