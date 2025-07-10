using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Company.Function;

public class CacheResponseToUserHttpTrigger
{
    private readonly ILogger<CacheResponseToUserHttpTrigger> _logger;
    private readonly IMemoryCache _memoryCache;

    public CacheResponseToUserHttpTrigger(
        ILogger<CacheResponseToUserHttpTrigger> logger,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
    }

    [Function("CacheResponseToUserHttpTrigger")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete")] HttpRequest req)
    {
        _logger.LogInformation("Cache HTTP trigger function processed a {Method} request.", req.Method);

        try
        {
            return req.Method.ToUpper() switch
            {
                "GET" => GetFromCache(req),
                "POST" => await SetToCache(req),
                "PUT" => await SetToCache(req),
                "DELETE" => DeleteFromCache(req),
                _ => new BadRequestObjectResult(new { error = "Unsupported HTTP method" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cache request");
            return new ObjectResult(new { error = ex.Message })
            {
                StatusCode = 500
            };
        }
    }

    private IActionResult GetFromCache(HttpRequest req)
    {
        var key = req.Query["key"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(key))
        {
            return new BadRequestObjectResult(new { 
                error = "Key parameter is required",
                usage = "GET /api/CacheResponseToUserHttpTrigger?key=yourkey"
            });
        }

        if (_memoryCache.TryGetValue(key, out var cachedValue))
        {
            var cacheInfo = cachedValue as CacheItem;
            
            _logger.LogInformation("Cache HIT for key: {Key}", key);
            
            return new OkObjectResult(new
            {
                key = key,
                value = cacheInfo?.Value,
                cached_at = cacheInfo?.CachedAt,
                expires_at = cacheInfo?.ExpiresAt,
                hit = true,
                ttl_seconds = cacheInfo?.ExpiresAt != null ? 
                    Math.Max(0, (cacheInfo.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds) : (double?)null
            });
        }
        else
        {
            _logger.LogInformation("Cache MISS for key: {Key}", key);
            
            return new NotFoundObjectResult(new
            {
                key = key,
                message = "Key not found in cache",
                hit = false
            });
        }
    }

    private async Task<IActionResult> SetToCache(HttpRequest req)
    {
        var key = req.Query["key"].FirstOrDefault();
        var ttlSeconds = req.Query["ttl"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(key))
        {
            return new BadRequestObjectResult(new { 
                error = "Key parameter is required",
                usage = "POST /api/CacheResponseToUserHttpTrigger?key=yourkey&ttl=300"
            });
        }

        // Read the request body for the value to cache
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        
        if (string.IsNullOrEmpty(requestBody))
        {
            return new BadRequestObjectResult(new { 
                error = "Request body is required for cache value",
                usage = "POST with JSON body containing the value to cache"
            });
        }

        // Parse TTL (default to 5 minutes if not provided)
        var ttl = TimeSpan.FromMinutes(5);
        if (!string.IsNullOrEmpty(ttlSeconds) && int.TryParse(ttlSeconds, out var seconds))
        {
            ttl = TimeSpan.FromSeconds(seconds);
        }

        // Try to parse JSON, but also support plain text
        object valueToCache;
        try
        {
            valueToCache = JsonSerializer.Deserialize<object>(requestBody) ?? requestBody;
        }
        catch
        {
            // If JSON parsing fails, store as plain text
            valueToCache = requestBody;
        }

        var cacheItem = new CacheItem
        {
            Value = valueToCache,
            CachedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
        };

        // Configure cache options
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            SlidingExpiration = TimeSpan.FromMinutes(1), // Reset TTL if accessed within 1 minute
            Priority = CacheItemPriority.Normal
        };

        // Add callback when item is removed
        cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            _logger.LogInformation("Cache item evicted: Key={Key}, Reason={Reason}", key, reason);
        });

        _memoryCache.Set(key, cacheItem, cacheOptions);
        
        _logger.LogInformation("Cached item with key: {Key}, TTL: {TTL} seconds", key, ttl.TotalSeconds);

        return new OkObjectResult(new
        {
            key = key,
            value = valueToCache,
            cached_at = cacheItem.CachedAt,
            expires_at = cacheItem.ExpiresAt,
            ttl_seconds = ttl.TotalSeconds,
            action = "cached"
        });
    }

    private IActionResult DeleteFromCache(HttpRequest req)
    {
        var key = req.Query["key"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(key))
        {
            return new BadRequestObjectResult(new { 
                error = "Key parameter is required",
                usage = "DELETE /api/CacheResponseToUserHttpTrigger?key=yourkey"
            });
        }

        var existed = _memoryCache.TryGetValue(key, out _);
        _memoryCache.Remove(key);
        
        _logger.LogInformation("Cache item removed: Key={Key}, Existed={Existed}", key, existed);

        return new OkObjectResult(new
        {
            key = key,
            action = "deleted",
            existed = existed
        });
    }
}

public class CacheItem
{
    public object? Value { get; set; }
    public DateTimeOffset CachedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}