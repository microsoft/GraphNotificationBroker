namespace GraphNotifications.Services
{
    /// <summary>
    /// 
    /// </summary> 
    public interface ICacheService
    {
        Task<bool> AddAsync<T>(string key, T value, TimeSpan? expiry = default(TimeSpan?));

        Task<T> GetAsync<T>(string key);

        Task<bool> DeleteAsync(string key);
    }
}