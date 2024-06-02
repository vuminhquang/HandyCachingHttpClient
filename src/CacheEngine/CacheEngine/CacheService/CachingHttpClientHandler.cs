using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CacheService
{
    public class CachingHttpClientHandler(IMemoryCache cache, ILogger<CachingHttpClientHandler> logger, IConfiguration configuration)
        : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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