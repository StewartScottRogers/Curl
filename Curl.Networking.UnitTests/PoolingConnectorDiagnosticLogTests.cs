using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// The reuse decision <see cref="PoolingConnector" /> writes to the target's diagnostic log at
/// <c>verbose</c>, component <c>connect</c> (ADR-0222, BL-920), naming the target by scheme,
/// host and port and never the proxy's credential.
/// </summary>
[TestClass]
public sealed class PoolingConnectorDiagnosticLogTests
{
    private readonly FakeConnector _inner = new();
    private readonly ManualTimeProvider _time = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task ConnectAsync_WhenAnIdleConnectionMatches_LogsItsReuseAtVerbose()
    {
        await using var pool = new PoolingConnector(_inner, _time);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var target = new ConnectTarget("origin.example", 80, UseTls: false) { PoolScheme = "http", DiagnosticLog = log };
        var first = await pool.ConnectAsync(target, CancellationToken.None);
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();

        Diagnostics.Arrange("target", "http://origin.example:80, an idle connection returned");

        ConnectResult second;
        using (Diagnostics.Phase("second connect"))
        {
            second = await pool.ConnectAsync(target, CancellationToken.None);
        }

        Diagnostics.Act("log lines", string.Join(" | ", log.Lines));
        Diagnostics.Assert("reused", true, second.IsReused);

        Assert.IsTrue(second.IsReused);
        CollectionAssert.AreEqual(
            new[]
            {
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Connect, "pool http://origin.example:80: new connection: no idle connection matches"),
                (DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Connect, "pool http://origin.example:80: reusing idle connection #0"),
            },
            log.Lines);
    }

    [TestMethod]
    public async Task ConnectAsync_ForATargetThatIsNeverPooled_LogsWhy()
    {
        await using var pool = new PoolingConnector(_inner, _time);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);

        Diagnostics.Arrange("target", "origin.example:80, no pool scheme");

        await pool.ConnectAsync(new ConnectTarget("origin.example", 80, UseTls: false) { DiagnosticLog = log }, CancellationToken.None);

        Diagnostics.Act("log lines", string.Join(" | ", log.Lines));
        Diagnostics.Assert("verbose connect lines", 1, log.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Connect).Length);

        CollectionAssert.AreEqual(
            new[] { "pool (unpooled)://origin.example:80: new connection: the target is never pooled" },
            log.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Connect));
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxyWithACredential_NeverLogsThePassword()
    {
        await using var pool = new PoolingConnector(_inner, _time);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, new NetworkCredential("user", "p00l-s3cret"));

        Diagnostics.Arrange("proxy", "http://proxy.example:3128 with a user and a password");

        await pool.ConnectAsync(new ConnectTarget("origin.example", 80, UseTls: false) { PoolScheme = "http", Proxy = proxy, DiagnosticLog = log }, CancellationToken.None);

        Diagnostics.Act("log lines", log.Lines.Count);
        Diagnostics.Assert("a line contains the password", false, log.Lines.Exists(line => line.Message.Contains("p00l-s3cret", StringComparison.Ordinal)));

        log.AssertNeverContains("p00l-s3cret");
    }

    [TestMethod]
    public async Task ConnectAsync_AtInfo_LogsNoPoolDecision()
    {
        await using var pool = new PoolingConnector(_inner, _time);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        Diagnostics.Arrange("log level", "info");

        await pool.ConnectAsync(new ConnectTarget("origin.example", 80, UseTls: false) { PoolScheme = "http", DiagnosticLog = log }, CancellationToken.None);

        Diagnostics.Act("log lines", log.Lines.Count);
        Diagnostics.Assert("log lines", 0, log.Lines.Count);
        Assert.IsEmpty(log.Lines);
    }
}
