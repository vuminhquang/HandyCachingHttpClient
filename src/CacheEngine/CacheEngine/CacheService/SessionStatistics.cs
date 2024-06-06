using Microsoft.Extensions.Logging;

namespace CacheService;

public class SessionStatistics
{
    public int CacheHits { get; set; } = 0;
    public int CacheMisses { get; set; } = 0;

    public void LogSummary(ILogger logger)
    {
        logger.LogInformation("Session Summary: Cache Hits: {CacheHits}, Cache Misses: {CacheMisses}", CacheHits, CacheMisses);
    }
}