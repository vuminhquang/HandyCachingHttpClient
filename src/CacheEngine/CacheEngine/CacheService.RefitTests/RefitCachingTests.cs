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
    private readonly WireMockServer server;
    private readonly ITestApi testApi;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<CachingHttpClientHandler> logger;

    public RefitCachingTests()
    {
        // Set up WireMock server
        server = WireMockServer.Start();
        server.Given(Request.Create().WithPath("/data").UsingGet())
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
        memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
        logger = serviceProvider.GetRequiredService<ILogger<CachingHttpClientHandler>>();

        // Configure HttpClient with CachingHttpClientHandler
        var handler = new CachingHttpClientHandler(memoryCache, logger, configuration)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(server.Url)
        };

        // Create Refit client
        testApi = RestService.For<ITestApi>(httpClient);
    }

    [Fact]
    public async Task GetDataAsync_ShouldReturnCachedResponse()
    {
        // Act - First request should hit the server
        var response1 = await testApi.GetDataAsync();

        // Assert - Check first response
        Assert.Equal("Mocked response", response1);
        Assert.Single(server.LogEntries);

        // Act - Second request should come from cache
        var response2 = await testApi.GetDataAsync();

        // Assert - Check second response
        Assert.Equal("Mocked response", response2);
        Assert.Single(server.LogEntries); // No new server log entry, indicating it's cached
    }

    public void Dispose()
    {
        server.Stop();
        server.Dispose();
    }
}