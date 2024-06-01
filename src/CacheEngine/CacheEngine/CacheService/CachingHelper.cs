using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CacheService;

public static class CachingHelper
{
    public static async Task<HttpResponseMessage> GetResponseWithCachingAsync(
        HttpRequestMessage request,
        IMemoryCache cache,
        ILogger logger,
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
        cache.Set(cacheKey, responseClone, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        return response;
    }
}