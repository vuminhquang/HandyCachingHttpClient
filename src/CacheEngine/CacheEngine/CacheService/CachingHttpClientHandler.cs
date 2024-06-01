using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CacheService
{
    public class CachingHttpClientHandler(IMemoryCache cache, ILogger<CachingHttpClientHandler> logger)
        : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cacheKey = request.RequestUri?.ToString() ?? string.Empty;

            if (cache.TryGetValue(cacheKey, out CachedHttpResponse? cachedResponse))
            {
                logger.LogInformation("Cache hit for {Url}", cacheKey);
                // if cached response is null, then exit if block to continue fetching from server
                if (cachedResponse == null) 
                {
                    logger.LogWarning("Cached response is null for {Url}. Fetching from server...", cacheKey);
                }
                else
                {
                    return cachedResponse.ToHttpResponseMessage();
                }
            }

            logger.LogInformation("Cache miss for {Url}. Fetching from server...", cacheKey);
            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return response;

            var responseClone = await CachedHttpResponse.FromHttpResponseMessageAsync(response);
            cache.Set(cacheKey, responseClone, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return responseClone.ToHttpResponseMessage();
        }
    }
}