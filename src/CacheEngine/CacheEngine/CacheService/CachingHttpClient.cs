namespace CacheService;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

public class CachingHttpClient(HttpMessageHandler handler, IMemoryCache cache, ILogger<CachingHttpClient> logger)
    : HttpClient(handler)
{
    public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cacheKey = request.RequestUri.ToString();
        if (cache.TryGetValue(cacheKey, out HttpResponseMessage cachedResponse))
        {
            logger.LogInformation("Cache hit for {Url}", cacheKey);
            return CloneHttpResponseMessage(cachedResponse);
        }

        logger.LogInformation("Cache miss for {Url}. Fetching from server...", cacheKey);
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return response;
        // Clone the response before caching it
        var responseClone = CloneHttpResponseMessage(response);
        cache.Set(cacheKey, responseClone, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });
        return responseClone;

    }

    private HttpResponseMessage CloneHttpResponseMessage(HttpResponseMessage response)
    {
        var clone = new HttpResponseMessage(response.StatusCode)
        {
            Content = response.Content,
            ReasonPhrase = response.ReasonPhrase,
            RequestMessage = response.RequestMessage,
            Version = response.Version
        };

        foreach (var header in response.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in response.Content.Headers)
        {
            clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}