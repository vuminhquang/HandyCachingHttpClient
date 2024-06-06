using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CacheService
{
    public class CachingHttpClientHandler : DelegatingHandler
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachingHttpClientHandler> _logger;
        private readonly IConfiguration _configuration;
        private readonly SessionStatistics? _sessionStatistics;

        public CachingHttpClientHandler(IMemoryCache cache, ILogger<CachingHttpClientHandler> logger, IConfiguration configuration, SessionStatistics? sessionStatistics = null)
        {
            _cache = cache;
            _logger = logger;
            _configuration = configuration;
            _sessionStatistics = sessionStatistics;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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