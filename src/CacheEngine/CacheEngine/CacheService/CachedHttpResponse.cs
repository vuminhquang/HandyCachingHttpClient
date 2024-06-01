using System.Net;

namespace CacheService;

public class CachedHttpResponse
{
    public HttpStatusCode StatusCode { get; set; }
    public string? ReasonPhrase { get; set; }
    public HttpRequestMessage? RequestMessage { get; set; }
    public Version? Version { get; set; }
    public Dictionary<string, IEnumerable<string>> Headers { get; set; } = new();
    public Dictionary<string, IEnumerable<string>> ContentHeaders { get; set; } = new();
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public static async Task<CachedHttpResponse> FromHttpResponseMessageAsync(HttpResponseMessage response)
    {
        var cachedResponse = new CachedHttpResponse
        {
            StatusCode = response.StatusCode,
            ReasonPhrase = response.ReasonPhrase,
            RequestMessage = response.RequestMessage,
            Version = response.Version,
            Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value),
            ContentHeaders = response.Content.Headers.ToDictionary(h => h.Key, h => h.Value),
            Content = await response.Content.ReadAsByteArrayAsync()
        };

        return cachedResponse;
    }

    public HttpResponseMessage ToHttpResponseMessage()
    {
        var response = new HttpResponseMessage(StatusCode)
        {
            ReasonPhrase = ReasonPhrase,
            RequestMessage = RequestMessage,
            Version = Version,
            Content = new ByteArrayContent(Content)
        };

        foreach (var header in Headers)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in ContentHeaders)
        {
            response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return response;
    }
}