

using GraphNotifications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Text;
using System.Threading;

namespace GraphNotifications.Services
{
    /// <summary>
    /// Implements connection to Redis
    /// </summary> 
    public class CacheService : ICacheService
    {
        private readonly ILogger<CacheService> _logger;
        private readonly IRedisFactory _redisFactory;
        private static readonly Encoding encoding = Encoding.UTF8;
        
        public CacheService(IRedisFactory redisFactory, ILogger<CacheService> logger)
        {
            _redisFactory = redisFactory;
            _logger = logger;
        }

        public async Task<bool> AddAsync<T>(string key, T value, TimeSpan? expiry = default(TimeSpan?))
        {
            try
            {
                var redis = _redisFactory.GetCache();
                if (redis == null) throw new ArgumentNullException("Redis Cache is null");

                _logger.LogInformation($"Adding value to redis {key}");
                // TODO move this out to it's own UTIL Class
                var jsonString = JsonConvert.SerializeObject(value);
                return await redis.StringSetAsync(key, encoding.GetBytes(jsonString), expiry);
            }
            catch (RedisConnectionException ex)
            {
                _redisFactory.ForceReconnect();
                _logger.LogError(ex, "Redis Connection Error");
                throw;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Redis Add Error for - {key}");
                throw;
            }
        }

        public async Task<T> GetAsync<T>(string key)
        {
            try
            {
                var redis = _redisFactory.GetCache();
                if (redis == null) throw new ArgumentNullException("Redis Cache is null");

                var value = await redis.StringGetAsync(key);
                if (!value.HasValue)
                {
                    return default(T);
                }
                return JsonConvert.DeserializeObject<T>(value);
            }
            catch (RedisConnectionException ex)
            {
                _redisFactory.ForceReconnect();
                _logger.LogError(ex, "Redis Connection Error");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Redis Get Error for - {key}");
                throw;
            }
        }

        public async Task<bool> DeleteAsync(string key)
        {
            try
            {
                var redis = _redisFactory.GetCache();
                if (redis == null) throw new ArgumentNullException("Redis Cache is null");

                return await redis.KeyDeleteAsync(key);
            }
            catch (RedisConnectionException ex)
            {
                _redisFactory.ForceReconnect();
                _logger.LogError(ex, "Redis Connection Error");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Redis Get Error for - {key}");
                throw;
            }
        }
    }
}