using System.Net;

namespace IndexDesk.UnitTests.Helpers;

public class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        return Task.FromResult(_handler(request));
    }

    public static TestHttpMessageHandler CreateJson(
        string jsonResponse,
        HttpStatusCode statusCode = HttpStatusCode.OK
    )
    {
        return new TestHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                jsonResponse,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        });
    }

    public static TestHttpMessageHandler CreateStatusCode(HttpStatusCode statusCode)
    {
        return new TestHttpMessageHandler(_ => new HttpResponseMessage(statusCode));
    }
}
