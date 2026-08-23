using System.Net;
using System.Text;

namespace IndexDesk.UnitTests.Helpers;

/// <summary>HTTP test double that records every request URL and answers via callback.</summary>
public sealed class CapturingHttpHandler : HttpMessageHandler
{
    private readonly Func<string, HttpResponseMessage> _responder;

    public CapturingHttpHandler(Func<string, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    /// <summary>URLs in request order — asserted for token placement / call counts.</summary>
    public List<string> RequestUrls { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var url = request.RequestUri!.ToString();
        RequestUrls.Add(url);
        return Task.FromResult(_responder(url));
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
