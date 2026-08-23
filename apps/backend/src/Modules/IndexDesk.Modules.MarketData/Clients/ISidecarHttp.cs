namespace IndexDesk.Modules.MarketData.Clients;

/// <summary>One sidecar <c>fetch</c> invocation.</summary>
public sealed record SidecarHttpRequest(
    string Url,
    string Method = "GET",
    string? Data = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    int? TimeoutSeconds = null
);

/// <summary>
/// Failure of one sidecar fetch carrying the sidecar error code verbatim
/// (<c>Scrape.WafBlocked</c>, <c>Sidecar.Timeout</c>, ...) so callers can
/// degrade per cause (e.g. PARTIAL_WARNING instead of a hard job failure).
/// </summary>
public sealed class SidecarHttpException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Generic WAF-safe HTTP transport over the Python sidecar's <c>fetch</c>
/// command (<c>curl_cffi impersonate="chrome"</c>). For hosts that reject
/// non-browser TLS fingerprints (Akamai), native HttpClient gets 403 while the
/// sidecar gets 200 — feeds opt in per source via configuration.
/// </summary>
public interface ISidecarHttp
{
    /// <summary>Fetches and returns the response body as text (HTML/JSON/CSV).</summary>
    Task<string> FetchTextAsync(
        SidecarHttpRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>Fetches binary payloads (XLSX) through the sidecar's base64 mode.</summary>
    Task<byte[]> FetchBinaryAsync(
        SidecarHttpRequest request,
        CancellationToken cancellationToken = default
    );
}
