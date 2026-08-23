using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IndexDesk.BuildingBlocks.Common.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

/// <summary>Outcome of one sidecar invocation, honoring the NDJSON process contract.</summary>
public sealed record SidecarRunResult(
    bool SpawnFailed,
    bool TimedOut,
    int ExitCode,
    IReadOnlyList<string> StdoutLines,
    string StderrTail,
    string? StderrErrorCode
);

/// <summary>
/// Spawns the Python provider sidecar (<c>tools/providers/sidecar</c>) through uv and
/// enforces its process contract: quote/dividend NDJSON on stdout, logs plus a single
/// <c>{"error":{...}}</c> envelope on stderr, exit codes 0 ok / 2 usage / 3 fetch /
/// 4 parse.
///
/// Configuration (bound once at construction):
/// - <c>Providers:Sidecar:UvPath</c> — uv executable (default <c>uv</c>);
/// - <c>Providers:Sidecar:ProjectPath</c> — sidecar project dir (default: resolved by
///   walking up from the working directory/base directory until
///   <c>tools/providers/sidecar/pyproject.toml</c> is found);
/// - <c>Providers:Sidecar:TimeoutSeconds</c> — per-invocation timeout (default 120 s);
///   on expiry the whole process tree is killed and the run maps to <c>Sidecar.Timeout</c>.
///
/// Secrets (TradingView cookie via argv, InfoMoney key via environment) are passed to
/// the child process but are never written to logs; stderr tails quoted in error
/// messages are truncated to keep envelopes small.
/// </summary>
public sealed class SidecarProcessRunner
{
    public const int ExitOk = 0;
    public const int ExitUsage = 2;
    public const int ExitFetch = 3;
    public const int ExitParse = 4;

    private const int MaxStderrTailChars = 4000;
    private const int MaxTailInErrorMessageChars = 300;

    private readonly string _executable;
    private readonly IReadOnlyList<string> _prefixArgs;
    private readonly TimeSpan _timeout;
    private readonly ILogger<SidecarProcessRunner> _logger;

    public SidecarProcessRunner(IConfiguration configuration, ILogger<SidecarProcessRunner> logger)
    {
        var uvPath = configuration["Providers:Sidecar:UvPath"];
        if (string.IsNullOrWhiteSpace(uvPath))
        {
            uvPath = "uv";
        }

        var projectPath = configuration["Providers:Sidecar:ProjectPath"];
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            projectPath = ResolveDefaultProjectPath();
        }

        var timeoutSeconds = 120;
        if (
            int.TryParse(configuration["Providers:Sidecar:TimeoutSeconds"], out var parsed)
            && parsed > 0
        )
        {
            timeoutSeconds = parsed;
        }

        _executable = uvPath;
        _prefixArgs = new[] { "run", "--project", projectPath!, "sidecar" };
        _timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _logger = logger;
    }

    /// <summary>Test seam: invoke an arbitrary executable with a fixed argument prefix.</summary>
    internal SidecarProcessRunner(
        string executable,
        IReadOnlyList<string> prefixArgs,
        TimeSpan timeout,
        ILogger<SidecarProcessRunner> logger
    )
    {
        _executable = executable;
        _prefixArgs = prefixArgs;
        _timeout = timeout;
        _logger = logger;
    }

    public TimeSpan Timeout => _timeout;

    /// <summary>
    /// Runs <c>&lt;prefixArgs&gt; sidecar &lt;sidecarArgs&gt;</c>, streaming stdout lines into
    /// the result. Never throws for process-level failures — those come back as
    /// <see cref="SidecarRunResult.SpawnFailed"/>/<see cref="SidecarRunResult.TimedOut"/>.
    /// </summary>
    public async Task<SidecarRunResult> RunAsync(
        IReadOnlyList<string> sidecarArgs,
        IDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default
    )
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in _prefixArgs.Concat(sidecarArgs))
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    startInfo.Environment[key] = value;
                }
            }
        }

        _logger.LogInformation(
            "[Sidecar] Spawning {Executable} ({TimeoutSeconds}s budget).",
            _executable,
            _timeout.TotalSeconds
        );

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex) // Win32Exception when uv is missing, etc. Args never logged (secrets).
        {
            _logger.LogError(ex, "[Sidecar] Failed to spawn {Executable}.", _executable);
            return new SidecarRunResult(true, false, -1, [], string.Empty, null);
        }

        if (process is null)
        {
            return new SidecarRunResult(true, false, -1, [], string.Empty, null);
        }

        try
        {
            var stdoutLines = new List<string>();
            var stderrSink = new StringBuilder();
            var stderrTask = ConsumeStderrAsync(process, stderrSink);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            timeoutCts.CancelAfter(_timeout);

            var timedOut = false;
            try
            {
                while (
                    await process
                        .StandardOutput.ReadLineAsync(timeoutCts.Token)
                        .ConfigureAwait(false)
                        is { } line
                )
                {
                    stdoutLines.Add(line);
                }

                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                await KillProcessTreeAsync(process).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled: still tear the child down, then rethrow.
                await KillProcessTreeAsync(process).ConfigureAwait(false);
                throw;
            }

            await stderrTask.ConfigureAwait(false);
            process.WaitForExit(); // flush async stderr events before reading the sink

            var tail = stderrSink.ToString();
            return new SidecarRunResult(
                SpawnFailed: false,
                TimedOut: timedOut,
                ExitCode: timedOut ? -1 : process.ExitCode,
                StdoutLines: stdoutLines,
                StderrTail: tail,
                StderrErrorCode: ParseStderrErrorCode(tail)
            );
        }
        finally
        {
            process.Dispose();
        }
    }

    /// <summary>
    /// Default failure mapping shared by all sidecar clients. Provider-specific codes
    /// (TradingView.AuthFailed, Scrape.WafBlocked, ...) take precedence and are applied
    /// by the clients themselves before falling back here.
    /// </summary>
    public Error MapFailure(SidecarRunResult result, string providerName)
    {
        if (result.SpawnFailed)
        {
            return Error.Failure(
                "Sidecar.SpawnFailed",
                $"Could not spawn the sidecar process for {providerName} "
                    + $"(executable '{_executable}' not found or not runnable)"
            );
        }

        if (result.TimedOut)
        {
            return Error.Failure(
                "Sidecar.Timeout",
                $"Sidecar timed out after {_timeout.TotalSeconds:F0}s "
                    + $"for {providerName}{DescribeTail(result)}"
            );
        }

        return result.ExitCode switch
        {
            ExitUsage => Error.Failure(
                "Sidecar.Usage",
                $"Sidecar rejected the arguments (exit 2) for {providerName}{DescribeTail(result)}"
            ),
            ExitParse => Error.Failure(
                "Sidecar.ParseError",
                $"Sidecar emitted invalid NDJSON (exit 4) for {providerName}{DescribeTail(result)}"
            ),
            ExitFetch => Error.Failure(
                "Sidecar.FetchFailed",
                $"Provider fetch failed in the sidecar (exit 3) for {providerName}"
                    + DescribeTail(result)
            ),
            _ => Error.Failure(
                "Sidecar.FetchFailed",
                $"Sidecar exited with unexpected code {result.ExitCode} for {providerName}"
                    + DescribeTail(result)
            ),
        };
    }

    private static string DescribeTail(SidecarRunResult result)
    {
        var trimmed = result.StderrTail?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.Length <= MaxTailInErrorMessageChars
            ? $": {trimmed}"
            : $": {trimmed[..MaxTailInErrorMessageChars]}…";
    }

    /// <summary>Last <c>{"error":{"code":"..."}}</c> envelope found on stderr, if any.</summary>
    private static string? ParseStderrErrorCode(string stderrTail)
    {
        string? lastCode = null;
        foreach (var rawLine in stderrTail.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith('{') || !line.Contains("\"error\""))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (
                    root.TryGetProperty("error", out var error)
                    && error.TryGetProperty("code", out var code)
                    && code.GetString() is { Length: > 0 } value
                )
                {
                    lastCode = value;
                }
            }
            catch (JsonException)
            {
                // Not an envelope (truncated line, stray JSON log) — keep scanning.
            }
        }

        return lastCode;
    }

    private static async Task ConsumeStderrAsync(Process process, StringBuilder sink)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                sink.AppendLine(line);
                if (sink.Length > MaxStderrTailChars * 2)
                {
                    sink.Remove(0, sink.Length - MaxStderrTailChars); // keep only the tail
                }
            }
        }
        catch (Exception)
        {
            // Reader dies with the process on kill — the partial tail is enough.
        }
    }

    private static async Task KillProcessTreeAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Already gone or access denied — nothing left to do.
        }

        try
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Best-effort reap.
        }
    }

    private static string ResolveDefaultProjectPath()
    {
        foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = Path.GetFullPath(root!);
            while (true)
            {
                var candidate = Path.Combine(dir, "tools", "providers", "sidecar");
                if (File.Exists(Path.Combine(candidate, "pyproject.toml")))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(dir)?.FullName;
                if (parent is null || parent == dir)
                {
                    break;
                }

                dir = parent;
            }
        }

        // Fallback: relative to the worker's content root (documented layout).
        return Path.Combine("tools", "providers", "sidecar");
    }
}
