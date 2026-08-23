using System.Text;
using FluentAssertions;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Offline tests for the sidecar process contract: fake executables (shell scripts)
/// stand in for <c>uv run ... sidecar</c> so no network and no uv are needed.
/// Linux-only by nature (bash scripts) — matches the WSL2 dev/CI environment.
/// </summary>
public class SidecarProcessRunnerTests : IDisposable
{
    private readonly string _tempDir;

    public SidecarProcessRunnerTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "sidecar-runner-tests",
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
            // Best-effort cleanup; leftover temp files are harmless.
        }
    }

    private string WriteScript(string name, string body)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, body, new UTF8Encoding(false));
        return path;
    }

    /// <summary>
    /// Runner wired to execute <paramref name="scriptPath"/> through bash, receiving the
    /// caller's sidecar args as positional parameters — the same shape the real
    /// <c>uv run ... sidecar</c> invocation produces.
    /// </summary>
    private static SidecarProcessRunner NewRunner(string scriptPath, TimeSpan? timeout = null) =>
        new(
            "/bin/bash",
            new[] { "-lc", $"exec bash '{scriptPath}' \"$@\"", "sidecar" },
            timeout ?? TimeSpan.FromSeconds(10),
            NullLogger<SidecarProcessRunner>.Instance
        );

    private Task<SidecarRunResult> RunAsync(
        string scriptPath,
        IReadOnlyList<string>? args = null,
        IDictionary<string, string?>? environment = null
    ) => NewRunner(scriptPath).RunAsync(args ?? Array.Empty<string>(), environment);

    [Fact]
    public async Task RunAsync_WithHappyScript_CapturesStdoutLinesAndExitZero()
    {
        var script = WriteScript(
            "happy.sh",
            """
            #!/usr/bin/env bash
            printf '%s\n' '{"ticker":"PETR4.SA","date":"2026-08-20","open":30.0,"high":31.0,"low":29.5,"close":30.7,"adj_close":31.2,"volume":1000}'
            printf '%s\n' '{"ticker":"PETR4.SA","date":"2026-08-21","open":30.5,"high":31.4,"low":30.1,"close":31.0,"adj_close":31.5,"volume":1200}'
            echo "[sidecar] emitted 2 lines" >&2
            exit 0
            """
        );

        var result = await RunAsync(script);

        result.SpawnFailed.Should().BeFalse();
        result.TimedOut.Should().BeFalse();
        result.ExitCode.Should().Be(0);
        result.StdoutLines.Should().HaveCount(2);
        result.StderrTail.Should().Contain("emitted 2 lines");
        result.StderrErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_WhenExecutableMissing_ReportsSpawnFailedWithoutThrowing()
    {
        var missingRunner = new SidecarProcessRunner(
            "/nonexistent/sidecar-test-missing-binary",
            Array.Empty<string>(),
            TimeSpan.FromSeconds(10),
            NullLogger<SidecarProcessRunner>.Instance
        );

        var result = await missingRunner.RunAsync(new[] { "anything" });

        result.SpawnFailed.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenScriptExitsThree_PropagatesExitCodeAndStderrEnvelopeCode()
    {
        var script = WriteScript(
            "fail3.sh",
            """
            #!/usr/bin/env bash
            echo '{"error":{"code":"Fetch.Failed","message":"provider down"}}' >&2
            exit 3
            """
        );

        var result = await RunAsync(script);

        result.ExitCode.Should().Be(SidecarProcessRunner.ExitFetch);
        result.StderrErrorCode.Should().Be("Fetch.Failed");
    }

    [Fact]
    public async Task RunAsync_WhenTimeoutExpires_KillsTreeAndFlagsTimeoutQuickly()
    {
        var script = WriteScript(
            "sleepy.sh",
            """
            #!/usr/bin/env bash
            sleep 60 &
            wait
            """
        );
        var startedAt = DateTime.UtcNow;

        var result = await NewRunner(script, TimeSpan.FromMilliseconds(500)).RunAsync([]);

        (DateTime.UtcNow - startedAt).Should().BeLessThan(TimeSpan.FromSeconds(5));
        result.TimedOut.Should().BeTrue();
        result.StdoutLines.Should().BeEmpty();
    }

    [Fact]
    public void MapFailure_MapsEachContractExitToItsDedicatedError()
    {
        var runner = NewRunner(WriteScript("noop.sh", "#!/usr/bin/env bash\n"));
        Error Map(int exitCode) =>
            runner.MapFailure(
                new SidecarRunResult(false, false, exitCode, [], "tail text", null),
                "YahooSidecar"
            );

        Map(SidecarProcessRunner.ExitUsage).Code.Should().Be("Sidecar.Usage");
        Map(SidecarProcessRunner.ExitParse).Code.Should().Be("Sidecar.ParseError");
        Map(SidecarProcessRunner.ExitFetch).Code.Should().Be("Sidecar.FetchFailed");
        Map(99).Code.Should().Be("Sidecar.FetchFailed");

        var spawn = runner.MapFailure(
            new SidecarRunResult(true, false, -1, [], string.Empty, null),
            "YahooSidecar"
        );
        spawn.Code.Should().Be("Sidecar.SpawnFailed");

        var timeout = runner.MapFailure(
            new SidecarRunResult(false, true, -1, [], string.Empty, null),
            "YahooSidecar"
        );
        timeout.Code.Should().Be("Sidecar.Timeout");
    }

    [Fact]
    public async Task RunAsync_ForwardsEnvironmentVariablesToChildProcess()
    {
        var script = WriteScript(
            "envcheck.sh",
            """
            #!/usr/bin/env bash
            if [ "$PROBE_SECRET" = "expected-value" ]; then
              echo '{"ticker":"X","date":"2026-08-21","rate":0.1,"type":"DIVIDENDO"}'
            else
              echo '{"error":{"code":"Fetch.Failed","message":"env not forwarded"}}' >&2
              exit 3
            fi
            """
        );

        var result = await RunAsync(
            script,
            environment: new Dictionary<string, string?> { ["PROBE_SECRET"] = "expected-value" }
        );

        result.ExitCode.Should().Be(0);
        result.StdoutLines.Should().HaveCount(1);
    }
}
