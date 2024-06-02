using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CacheService
{
    public class CachingHttpClient(HttpMessageHandler handler, IMemoryCache cache, ILogger<CachingHttpClient> logger, IConfiguration configuration)
        : HttpClient(handler)
    {
        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return CachingHelper.GetResponseWithCachingAsync(
                request,
                cache,
                logger,
                configuration,
                base.SendAsync,
                cancellationToken);
        }
    }
}