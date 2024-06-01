using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CacheService;

public class CachingHttpClientHandler : DelegatingHandler
{
    private readonly IMemoryCache cache;
    private readonly ILogger<CachingHttpClientHandler> logger;

    public CachingHttpClientHandler(IMemoryCache cache, ILogger<CachingHttpClientHandler> logger)
    {
        this.cache = cache;
        this.logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cacheKey = request.RequestUri.ToString();
        if (cache.TryGetValue(cacheKey, out HttpResponseMessage cachedResponse))
        {
            logger.LogInformation("Cache hit for {Url}", cacheKey);
            return await CloneHttpResponseMessageAsync(cachedResponse);
        }

        logger.LogInformation("Cache miss for {Url}. Fetching from server...", cacheKey);
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return response;

        // Clone the response before caching it
        var responseClone = await CloneHttpResponseMessageAsync(response);
        cache.Set(cacheKey, responseClone, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });

        return responseClone;
    }

    private static async Task<HttpResponseMessage> CloneHttpResponseMessageAsync(HttpResponseMessage response)
    {
        var clone = new HttpResponseMessage(response.StatusCode)
        {
            ReasonPhrase = response.ReasonPhrase,
            RequestMessage = response.RequestMessage,
            Version = response.Version
        };

        if (response.Content != null)
        {
            // Read the content into a byte array buffer before cloning
            var contentBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in response.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in response.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}