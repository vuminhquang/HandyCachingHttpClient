using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CacheService
{
    public class CachingHttpClientHandler : DelegatingHandler
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachingHttpClientHandler> _logger;

        public CachingHttpClientHandler(IMemoryCache cache, ILogger<CachingHttpClientHandler> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return CachingHelper.GetResponseWithCachingAsync(
                request,
                _cache,
                _logger,
                base.SendAsync,
                cancellationToken);
        }
    }
}