using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CacheService;

public static class CachingHelper
{
    public static async Task<HttpResponseMessage> GetResponseWithCachingAsync(
        HttpRequestMessage request,
        IMemoryCache cache,
        ILogger logger,
        IConfiguration configuration,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendRequest,
        CancellationToken cancellationToken)
    {
        var cacheKey = request.RequestUri?.ToString() ?? string.Empty;

        if (cache.TryGetValue(cacheKey, out CachedHttpResponse? cachedResponse))
        {
            logger.LogInformation("Cache hit for {Url}", cacheKey);
            if (cachedResponse != null)
            {
                return cachedResponse.ToHttpResponseMessage();
            }

            logger.LogWarning("Cached response is null for {Url}. Fetching from server...", cacheKey);
        }
        else
        {
            logger.LogInformation("Cache miss for {Url}. Fetching from server...", cacheKey);
        }

        var response = await sendRequest(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return response;

        var responseClone = await CachedHttpResponse.FromHttpResponseMessageAsync(response);

        // Read the cache expiration from configuration
        var cacheExpirationMinutes = configuration.GetValue<int>("CacheExpirationMinutes", 5);
        cache.Set(cacheKey, responseClone, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheExpirationMinutes)
        });

        return response;
    }
}