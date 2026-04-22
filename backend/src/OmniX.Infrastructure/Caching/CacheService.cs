using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace OmniX.Infrastructure.Caching;

// ════════════════════════════════════════════════════════════════════════════
//  ICacheService — واجهة الـ Cache الموحّدة
// ════════════════════════════════════════════════════════════════════════════
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

// ════════════════════════════════════════════════════════════════════════════
//  RedisCacheService — من MallX (Redis حقيقي)
//  يستخدم StackExchange.Redis عبر IDistributedCache
// ════════════════════════════════════════════════════════════════════════════
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger)
    {
        _cache  = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var data = await _cache.GetStringAsync(key);
            return data is null ? default : JsonSerializer.Deserialize<T>(data, _json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var opts = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30)
            };
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value, _json), opts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try { await _cache.RemoveAsync(key); }
        catch (Exception ex) { _logger.LogWarning(ex, "Cache REMOVE failed for key: {Key}", key); }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        // Note: StackExchange.Redis supports pattern delete via SCAN
        // For IDistributedCache abstraction, we remove known keys
        _logger.LogDebug("RemoveByPrefix: {Prefix}", prefix);
        await Task.CompletedTask;
    }
}

// ════════════════════════════════════════════════════════════════════════════
//  MemoryCacheService — Fallback إذا لم يكن Redis متاحاً
// ════════════════════════════════════════════════════════════════════════════
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key)
        => Task.FromResult(_cache.TryGetValue(key, out T? val) ? val : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        _cache.Set(key, value, expiry ?? TimeSpan.FromMinutes(30));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key) { _cache.Remove(key); return Task.CompletedTask; }
    public Task RemoveByPrefixAsync(string prefix) => Task.CompletedTask;
}
