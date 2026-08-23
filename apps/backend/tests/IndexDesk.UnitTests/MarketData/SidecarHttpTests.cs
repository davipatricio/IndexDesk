using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Ingestion.Holdings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Offline tests for <see cref="SidecarHttp"/> (generic WAF-safe fetch over the
/// Python sidecar's <code>fetch</code> command) and for the It Now holdings
/// feed's sidecar/native transport switch
/// (<c>Providers:Holdings:ItNow:Transport</c>, default sidecar).
/// A fake shell script plays the sidecar process; argv construction is asserted
/// from what the child receives.
/// </summary>
public class SidecarHttpTests : IDisposable
{
    private readonly string _tempDir;

    public SidecarHttpTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "sidecar-http-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    private static SidecarProcessRunner NewRunner(string scriptPath) =>
        new(
            "/bin/bash",
            new[] { "-lc", $"exec bash '{scriptPath}' \"$@\"", "sidecar" },
            TimeSpan.FromSeconds(10),
            NullLogger<SidecarProcessRunner>.Instance
        );

    private string WriteScript(string name, string body)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, body);
        return path;
    }

    /// <summary>Script prologue that captures the child's argv into the temp dir.</summary>
    private string CaptureArgv() =>
        $"#!/usr/bin/env bash\nprintf '%s\\n' \"$@\" > '{Path.Combine(_tempDir, "argv.txt")}'\n";

    [Fact]
    public async Task FetchText_BuildsExpectedArgv_AndReturnsBody()
    {
        const string body = "<html>composicao ok</html>";
        var script = WriteScript(
            "fetch.sh",
            $$"""
            {{CaptureArgv()}}
            cat <<'EOF'
            {{body}}
            EOF
            """
        );
        var http = new SidecarHttp(NewRunner(script));
        var request = new SidecarHttpRequest(
            "https://www.itnow.com.br/history-api-json/?type=composicoes-indices&fundo=BRBOVVCTF009",
            Method: "POST",
            Headers: new Dictionary<string, string> { ["Accept"] = "application/json" },
            TimeoutSeconds: 20
        );

        var result = await http.FetchTextAsync(request);

        result.Should().Be(body);
        var argv = await File.ReadAllTextAsync(Path.Combine(_tempDir, "argv.txt"));
        argv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Should()
            .Equal(
                "fetch",
                "--url",
                request.Url,
                "--method",
                "POST",
                "--header",
                "Accept: application/json",
                "--timeout-s",
                "20"
            );
    }

    [Fact]
    public async Task FetchBinary_RoundtripsBase64PayloadExactly()
    {
        // ZIP magic + non-UTF8 bytes: proves byte fidelity through the text pipe.
        var raw = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0xFF, 0x00, 0xFE, 0x7F, 0x42 };
        var encoded = Convert.ToBase64String(raw);
        var script = WriteScript(
            "fetch_b64.sh",
            $"""
            #!/usr/bin/env bash
            printf '%s\n' "{encoded}"
            """
        );

        var http = new SidecarHttp(NewRunner(script));

        var bytes = await http.FetchBinaryAsync(
            new SidecarHttpRequest("https://x.test/holdings.xlsx")
        );

        bytes.Should().Equal(raw);
    }

    [Fact]
    public async Task WafBlockedEnvelope_ThrowsWithSidecarErrorCode()
    {
        var script = WriteScript(
            "waf.sh",
            """
            #!/usr/bin/env bash
            echo '{"error":{"code":"Scrape.WafBlocked","message":"WAF blocked GET https://www.itnow.com.br/bovv11/composicao/ (HTTP 403)","status":403}}' >&2
            exit 3
            """
        );

        var http = new SidecarHttp(NewRunner(script));

        var act = () =>
            http.FetchTextAsync(
                new SidecarHttpRequest("https://www.itnow.com.br/bovv11/composicao/")
            );

        var exception = await act.Should().ThrowAsync<SidecarHttpException>();
        exception.Which.Code.Should().Be("Scrape.WafBlocked");
        exception.Which.Message.Should().Contain("(HTTP 403)");
    }

    [Fact]
    public async Task ExitThreeWithoutEnvelope_MapsToFetchFailed()
    {
        var script = WriteScript(
            "boom.sh",
            """
            #!/usr/bin/env bash
            echo "connection reset by peer" >&2
            exit 3
            """
        );

        var http = new SidecarHttp(NewRunner(script));

        var act = () => http.FetchTextAsync(new SidecarHttpRequest("https://down.test/a"));

        (await act.Should().ThrowAsync<SidecarHttpException>())
            .Which.Code.Should()
            .Be("Sidecar.FetchFailed");
    }

    [Fact]
    public async Task UnsupportedMethod_IsUsageRejectedBeforeSpawning()
    {
        var spawnProbe = WriteScript(
            "never.sh",
            "#!/usr/bin/env bash\necho 'must not run' >&2\nexit 1\n"
        );
        var http = new SidecarHttp(NewRunner(spawnProbe));

        var act = () =>
            http.FetchTextAsync(new SidecarHttpRequest("https://x.test/", Method: "PUT"));

        (await act.Should().ThrowAsync<SidecarHttpException>())
            .Which.Code.Should()
            .Be("Usage.Invalid");
    }
}

/// <summary>Transport seam assertions for <see cref="ItNowHoldingsFeed"/>.</summary>
public class ItNowFeedTransportTests
{
    private sealed class StubSidecarHttp : ISidecarHttp
    {
        public List<SidecarHttpRequest> Requests { get; } = [];
        public string Body { get; set; } = "<html>stub</html>";

        public Task<string> FetchTextAsync(
            SidecarHttpRequest request,
            CancellationToken cancellationToken = default
        )
        {
            Requests.Add(request);
            return Task.FromResult(Body);
        }

        public Task<byte[]> FetchBinaryAsync(
            SidecarHttpRequest request,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException("not exercised by this stub");
    }

    private static HttpClient NewNativeClient() =>
        new() { BaseAddress = new Uri("https://www.itnow.com.br/") };

    private static IConfiguration BuildConfig(string? transport = null)
    {
        var values = new Dictionary<string, string?>();
        if (transport is not null)
        {
            values["Providers:Holdings:ItNow:Transport"] = transport;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public async Task DefaultTransport_RoutesThroughSidecar_WithAbsoluteUrlAndPostMethod()
    {
        using var httpClient = NewNativeClient();
        var stub = new StubSidecarHttp();
        var feed = new ItNowHoldingsFeed(httpClient, stub, BuildConfig());

        var html = await feed.GetCompositionHtmlAsync("BOVV11");
        var json = await feed.PostCompositionJsonAsync("BOVV11", fundCode: "BRBOVVCTF009");

        html.Should().Be("<html>stub</html>");
        json.Should().Be("<html>stub</html>");
        stub.Requests.Should().HaveCount(2);
        stub.Requests[0].Url.Should().Be("https://www.itnow.com.br/bovv11/composicao/");
        stub.Requests[0].Method.Should().Be("GET");
        stub.Requests[0].TimeoutSeconds.Should().Be(20);
        stub.Requests[1]
            .Url.Should()
            .Be(
                "https://www.itnow.com.br/history-api-json/?type=composicoes-indices&fundo=BRBOVVCTF009"
            );
        stub.Requests[1].Method.Should().Be("POST");
    }

    [Fact]
    public async Task NativeConfig_BypassesSidecarCompletely()
    {
        // Unreachable endpoint: whichever failure shape the socket layer picks
        // (refused or client timeout), the point is that the sidecar is never called.
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:1/"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var stub = new StubSidecarHttp();
        var feed = new ItNowHoldingsFeed(httpClient, stub, BuildConfig("native"));

        var act = () => feed.GetCompositionHtmlAsync("BOVV11");

        await act.Should().ThrowAsync<Exception>(); // native socket attempted...
        stub.Requests.Should().BeEmpty(); // ...and the sidecar never saw anything
    }

    [Fact]
    public async Task WithoutRegisteredSidecar_FallsBackToNative()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:1/"),
            Timeout = TimeSpan.FromSeconds(2),
        };
        var feed = new ItNowHoldingsFeed(httpClient); // hand-built instance, no DI

        var act = () => feed.GetCompositionHtmlAsync("BOVV11");

        await act.Should().ThrowAsync<Exception>();
    }
}
