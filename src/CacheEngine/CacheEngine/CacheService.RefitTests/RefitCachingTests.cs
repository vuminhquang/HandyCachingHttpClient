using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace CacheService.RefitTests;

public class RefitCachingTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly ITestApi _testApi;
    private readonly SessionStatistics _sessionStatistics;

    public RefitCachingTests()
    {
        // Set up WireMock server
        _server = WireMockServer.Start();
        _server.Given(Request.Create().WithPath("/data").UsingGet())
              .RespondWith(Response.Create().WithStatusCode(200).WithBody("Mocked response"));

        // Set up Dependency Injection
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddMemoryCache();
        serviceCollection.AddLogging();
        
        // Add configuration for cache expiration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "CacheExpirationMinutes", "5" } // or any other value you need
            })
            .Build();
        serviceCollection.AddSingleton<IConfiguration>(configuration);
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<CachingHttpClientHandler>>();

        _sessionStatistics = new();
        
        // Configure HttpClient with CachingHttpClientHandler
        var handler = new CachingHttpClientHandler(memoryCache, logger, configuration, _sessionStatistics)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(_server.Url)
        };

        // Create Refit client
        _testApi = RestService.For<ITestApi>(httpClient);
    }

    [Fact]
    public async Task GetDataAsync_ShouldReturnCachedResponse()
    {
        // Act - First request should hit the server
        var response1 = await _testApi.GetDataAsync();

        // Assert - Check first response
        Assert.Equal("Mocked response", response1);
        Assert.Single(_server.LogEntries);

        // Act - Second request should come from cache
        var response2 = await _testApi.GetDataAsync();

        // Assert - Check second response
        Assert.Equal("Mocked response", response2);
        Assert.Single(_server.LogEntries); // No new server log entry, indicating it's cached
        
        // Assert - Check session statistics
        Assert.Equal(1, _sessionStatistics.CacheMisses);
        Assert.Equal(1, _sessionStatistics.CacheHits);
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}