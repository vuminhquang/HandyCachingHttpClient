using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CacheService
{
    public class CachingHttpClient(HttpMessageHandler handler, IMemoryCache cache, ILogger<CachingHttpClient> logger)
        : HttpClient(handler)
    {
        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return CachingHelper.GetResponseWithCachingAsync(
                request,
                cache,
                logger,
                base.SendAsync,
                cancellationToken);
        }
    }
}