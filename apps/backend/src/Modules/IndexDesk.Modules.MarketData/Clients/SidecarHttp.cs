namespace IndexDesk.Modules.MarketData.Clients;

/// <summary>
/// Default <see cref="ISidecarHttp"/>: spawns
/// <c>uv run sidecar fetch --url ... [--method] [--data] [--header K: V] [--b64]</c>
/// through the shared <see cref="SidecarProcessRunner"/>. Success returns the
/// body (stdout); any non-zero exit throws <see cref="SidecarHttpException"/>
/// with the sidecar's own error code (<c>Scrape.WafBlocked</c> for 403 WAF
/// blocks, otherwise <c>Sidecar.*</c> mappings). Stdout is line-oriented, so
/// text bodies are rejoined with LF — harmless for HTML/JSON/CSV parsers.
/// </summary>
public sealed class SidecarHttp(SidecarProcessRunner runner) : ISidecarHttp
{
    public async Task<string> FetchTextAsync(
        SidecarHttpRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var lines = await RunAsync(request, binary64: false, cancellationToken);
        return string.Join("\n", lines);
    }

    public async Task<byte[]> FetchBinaryAsync(
        SidecarHttpRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var lines = await RunAsync(request, binary64: true, cancellationToken);
        var joined = string.Join(string.Empty, lines);
        try
        {
            return Convert.FromBase64String(joined.Trim());
        }
        catch (FormatException ex)
        {
            throw new SidecarHttpException(
                "Sidecar.ParseError",
                $"sidecar fetch --b64 returned a payload that is not valid base64 "
                    + $"for {request.Url}: {ex.Message}"
            );
        }
    }

    private async Task<IReadOnlyList<string>> RunAsync(
        SidecarHttpRequest request,
        bool binary64,
        CancellationToken cancellationToken
    )
    {
        var method = request.Method.ToUpperInvariant();
        if (method is not ("GET" or "POST"))
        {
            throw new SidecarHttpException(
                "Usage.Invalid",
                $"sidecar fetch supports GET/POST only, got '{request.Method}'"
            );
        }

        var args = new List<string> { "fetch", "--url", request.Url, "--method", method };
        if (request.Headers is not null)
        {
            foreach (var (name, value) in request.Headers)
            {
                args.Add("--header");
                args.Add($"{name}: {value}");
            }
        }

        if (!string.IsNullOrEmpty(request.Data))
        {
            args.Add("--data");
            args.Add(request.Data);
        }

        if (request.TimeoutSeconds is { } timeoutSeconds)
        {
            args.Add("--timeout-s");
            args.Add(timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (binary64)
        {
            args.Add("--b64");
        }

        var result = await runner.RunAsync(args, environment: null, cancellationToken);

        if (result.SpawnFailed)
        {
            throw new SidecarHttpException(
                "Sidecar.SpawnFailed",
                $"Could not spawn the sidecar process for fetch {request.Url}"
            );
        }

        if (result.TimedOut)
        {
            throw new SidecarHttpException(
                "Sidecar.Timeout",
                $"sidecar fetch timed out for {request.Url}"
            );
        }

        if (result.ExitCode != SidecarProcessRunner.ExitOk)
        {
            // Provider-specific codes from the stderr envelope win (Scrape.WafBlocked...).
            var code = result.StderrErrorCode ?? MapExitCode(result.ExitCode);
            throw new SidecarHttpException(
                code,
                $"sidecar fetch failed for {request.Url} (exit {result.ExitCode})"
                    + DescribeTail(result.StderrTail)
            );
        }

        return result.StdoutLines;
    }

    private static string MapExitCode(int exitCode) =>
        exitCode switch
        {
            SidecarProcessRunner.ExitUsage => "Usage.Invalid",
            SidecarProcessRunner.ExitParse => "Sidecar.ParseError",
            _ => "Sidecar.FetchFailed",
        };

    private static string DescribeTail(string? stderrTail)
    {
        var trimmed = stderrTail?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.Length <= 300 ? $": {trimmed}" : $": {trimmed[..300]}…";
    }
}
