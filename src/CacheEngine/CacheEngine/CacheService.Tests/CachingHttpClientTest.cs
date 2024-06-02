using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;

namespace CacheService.Tests;

[TestSubject(typeof(CachingHttpClient))]
public class CachingHttpClientTests
{
    private readonly IMemoryCache _cache;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly ILogger<CachingHttpClient> _logger;
    private readonly IConfiguration _config;

    public CachingHttpClientTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var services = new ServiceCollection();
        services.AddLogging();
        
        // Add configuration for cache expiration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "CacheExpirationMinutes", "1" } // or any other value you need
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        
        var serviceProvider = services.BuildServiceProvider();
        _logger = serviceProvider.GetRequiredService<ILogger<CachingHttpClient>>();
        
        _config = serviceProvider.GetRequiredService<IConfiguration>();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnResponseFromCache_WhenCalledMultipleTimes()
    {
        // Arrange
        var url = "https://api.example.com/data";
        var cachedResponse = "cached response";

        _httpMessageHandlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(CreateResponseMessage())
            .ReturnsAsync(CreateResponseMessage())
            .ReturnsAsync(CreateResponseMessage());

        using var cachingHttpClient = new CachingHttpClient(_httpMessageHandlerMock.Object, _cache, _logger, _config);
        using var normalHttpClient = new HttpClient(_httpMessageHandlerMock.Object);

        // Act
        // response 1 & 2 could use same request as the request is not sent when called the 2nd time
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response1 = await cachingHttpClient.SendAsync(request, CancellationToken.None);
        var response2 = await cachingHttpClient.SendAsync(request, CancellationToken.None);
        // the normal response should create new request
        var request2 = new HttpRequestMessage(HttpMethod.Get, url);
        var normalResponse = await normalHttpClient.SendAsync(request2, CancellationToken.None);
        
        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        Assert.Equal(content1, content2);
        
        var normalContent = await normalResponse.Content.ReadAsStringAsync();
        Assert.NotEqual(content1, normalContent);
        
        // Verify that the HTTP client was called only twice (once for response 1 & 2, once for normal response)
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
        return;

        HttpResponseMessage CreateResponseMessage()
        {
            var httpResponseMessage = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"{cachedResponse} {Guid.NewGuid()}"),
            };
            return httpResponseMessage;
        }
    }

    [Fact]
    public async Task SendAsync_ShouldFetchNewResponse_WhenCacheExpires()
    {
        // Arrange
        var url = "https://api.example.com/data";
        var initialResponse = "initial response";
        var updatedResponse = "updated response";
        var initialHttpResponseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(initialResponse),
        };

        var updatedHttpResponseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(updatedResponse),
        };

        _httpMessageHandlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(initialHttpResponseMessage)
            .ReturnsAsync(updatedHttpResponseMessage);

        var cachingHttpClient = new CachingHttpClient(_httpMessageHandlerMock.Object, _cache, _logger, _config);

        // Act
        var request1 = new HttpRequestMessage(HttpMethod.Get, url);
        var response1 = await cachingHttpClient.SendAsync(request1, CancellationToken.None);
        var content1 = await response1.Content.ReadAsStringAsync();

        // Wait for cache expiration (adjust the delay based on your cache expiration settings)
        await Task.Delay(TimeSpan.FromMinutes(2));

        var request2 = new HttpRequestMessage(HttpMethod.Get, url);
        var response2 = await cachingHttpClient.SendAsync(request2, CancellationToken.None);
        var content2 = await response2.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(initialResponse, content1);
        Assert.Equal(updatedResponse, content2);

        // Verify that the HTTP client was called twice
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ShouldNotCacheErrorResponse()
    {
        // Arrange
        var url = "https://api.example.com/data";
        var errorHttpResponseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.InternalServerError,
            Content = new StringContent("error response"),
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(errorHttpResponseMessage);

        var cachingHttpClient = new CachingHttpClient(_httpMessageHandlerMock.Object, _cache, _logger, _config);

        // Act
        var request1 = new HttpRequestMessage(HttpMethod.Get, url);
        var request2 = new HttpRequestMessage(HttpMethod.Get, url);        
        var response1 = await cachingHttpClient.SendAsync(request1, CancellationToken.None);
        var response2 = await cachingHttpClient.SendAsync(request2, CancellationToken.None);

        // Assert
        var content1 = await response1.Content.ReadAsStringAsync();
        var content2 = await response2.Content.ReadAsStringAsync();
        Assert.Equal("error response", content1);
        Assert.Equal("error response", content2);

        // Verify that the HTTP client was called twice, indicating no caching
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}