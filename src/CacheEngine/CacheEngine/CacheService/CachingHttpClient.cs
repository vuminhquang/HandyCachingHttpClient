using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CacheService
{
    public class CachingHttpClient : HttpClient
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachingHttpClient> _logger;
        private readonly IConfiguration _configuration;
        private readonly SessionStatistics? _sessionStatistics;

        public CachingHttpClient(HttpMessageHandler handler, IMemoryCache cache, ILogger<CachingHttpClient> logger, IConfiguration configuration, SessionStatistics? sessionStatistics = null)
            : base(handler)
        {
            _cache = cache;
            _logger = logger;
            _configuration = configuration;
            _sessionStatistics = sessionStatistics;
        }

        public override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await CachingHelper.GetResponseWithCachingAsync(
                request,
                _cache,
                _logger,
                _configuration,
                _sessionStatistics,
                base.SendAsync,
                cancellationToken);

            _sessionStatistics?.LogSummary(_logger);

            return response;
        }
    }
}