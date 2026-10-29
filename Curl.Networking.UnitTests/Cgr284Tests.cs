using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-284 of the third-party gap report (BL-3813): curl 8.22.0 ends a <c>-m</c> that passes while a
/// host name is still being resolved (a DoH server that never answers) with
/// <c>Resolving timed out after N milliseconds</c>, not <c>Connection timed out</c>.
/// </summary>
[TestClass]
public sealed class Cgr284Tests
{
    [TestMethod]
    public async Task ConnectAsync_LimitPassesWhileResolving_FailsWithResolvingTimedOut()
    {
        var clock = new ManualTimeProvider();
        var connector = new TcpConnector(
            new HangingDnsResolver(() => clock.Advance((long)TcpConnector.DefaultConnectTimeout.TotalMilliseconds)),
            new FakeTcpDialer(),
            new FakeTlsProvider(),
            clock);

        var result = await connector.ConnectAsync(new ConnectTarget("foo.test", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Resolving timed out after 300000 milliseconds", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_CallerCancelsBeforeLimitWhileResolving_ThrowsAndReportsNoTimeout()
    {
        using var cancel = new CancellationTokenSource();
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(
            new HangingDnsResolver(() => cancel.Cancel()),
            new FakeTcpDialer(),
            new FakeTlsProvider(),
            new ManualTimeProvider());

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await connector.ConnectAsync(new ConnectTarget("foo.test", 80, UseTls: false) { Events = events }, cancel.Token));

        Assert.IsFalse(events.Info.Exists(line => line.Contains("Resolving timed out", StringComparison.Ordinal)));
    }

    private sealed class HangingDnsResolver(Action interrupt) : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            interrupt();
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([]);
        }
    }
}