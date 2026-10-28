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
    public async Task ConnectAsync_CallerCancelsWhileResolving_FailsWithResolvingTimedOut()
    {
        using var cancel = new CancellationTokenSource();
        var connector = new TcpConnector(
            new HangingDnsResolver(cancel),
            new FakeTcpDialer(),
            new FakeTlsProvider(),
            new ManualTimeProvider());

        var result = await connector.ConnectAsync(new ConnectTarget("foo.test", 80, UseTls: false), cancel.Token);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Resolving timed out after 0 milliseconds", result.ErrorMessage);
    }

    private sealed class HangingDnsResolver(CancellationTokenSource cancel) : IDnsResolver
    {
        public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            await cancel.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }
    }
}
