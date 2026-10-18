using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the default members of <see cref="IPendingConnection" />, which let an implementation
/// leave them out.
/// </summary>
[TestClass]
public sealed class IPendingConnectionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void HasConnectionWaiting_WhenNotOverridden_IsFalse()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IPendingConnection pending = new MinimalPendingConnection();
        diagnostics.Arrange("pending connection", nameof(MinimalPendingConnection));

        bool waiting = pending.HasConnectionWaiting;

        diagnostics.Act("has connection waiting", waiting);
        diagnostics.Assert("has connection waiting", false, waiting);
        Assert.IsFalse(waiting);
    }

    private sealed class MinimalPendingConnection : IPendingConnection
    {
        public EndPoint LocalEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 0);

        public ValueTask<ConnectResult> AcceptAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(ConnectResult.Failed(CurlExitCode.FtpAcceptFailed, "not used"));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
