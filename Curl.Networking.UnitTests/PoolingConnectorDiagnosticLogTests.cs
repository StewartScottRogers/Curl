using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public async Task ConnectAsync_WhenAnIdleConnectionMatches_LogsItsReuseAtVerbose()
    {
        await using var pool = new PoolingConnector(_inner, _time);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var target = new ConnectTarget("origin.example", 80, UseTls: false) { PoolScheme = "http", DiagnosticLog = log };
        var first = await pool.ConnectAsync(target, CancellationToken.None);
        first.Connection!.MarkReusable();
        await first.Connection.DisposeAsync();

        var second = await pool.ConnectAsync(target, CancellationToken.None);

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

        await pool.ConnectAsync(new ConnectTarget("origin.example", 80, UseTls: false) { DiagnosticLog = log }, CancellationToken.None);

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

        await pool.ConnectAsync(new ConnectTarget("origin.example", 80, UseTls: false) { PoolScheme = "http", Proxy = proxy, DiagnosticLog = log }, CancellationToken.None);

        log.AssertNeverContains("p00l-s3cret");
    }

    [TestMethod]
    public async Task ConnectAsync_AtInfo_LogsNoPoolDecision()
    {
        await using var pool = new PoolingConnector(_inner, _time);
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);

        await pool.ConnectAsync(new ConnectTarget("origin.example", 80, UseTls: false) { PoolScheme = "http", DiagnosticLog = log }, CancellationToken.None);

        Assert.IsEmpty(log.Lines);
    }
}
