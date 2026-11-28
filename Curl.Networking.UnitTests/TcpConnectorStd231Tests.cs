using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins STD-231: a TLS handshake the connect limit or <c>-m</c> ends had its TCP connection open, which curl 8.21.0
/// and 8.22.0 still count in <c>%{num_connects}</c> (measured, BL-6028 Notes); a dial that never connected is not.
/// </summary>
[TestClass]
public sealed class TcpConnectorStd231Tests
{
    private static readonly IPAddress Loopback = IPAddress.Loopback;

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [TestMethod]
    public async Task ConnectAsync_HandshakeEndedByTheConnectLimit_SaysTheTcpConnectionWasOpen()
    {
        var time = new ManualTimeProvider();
        var tlsProvider = new StallingTlsProvider { OnStalled = () => time.Advance(1006) };
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider, time, connectTimeout: OneSecond);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 29990, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsTrue(result.IsTcpConnectedBeforeFailure);
        Assert.AreEqual(new IPEndPoint(Loopback, 29990), result.RemoteEndPointBeforeFailure);
    }

    [TestMethod]
    public async Task ConnectAsync_DialEndedByTheConnectLimit_SaysNoTcpConnectionWasOpen()
    {
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => time.Advance(1001) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: OneSecond);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 29990, UseTls: true), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.IsFalse(result.IsTcpConnectedBeforeFailure);
    }

    [TestMethod]
    public async Task ConnectAsync_HandshakeCancelledByTheCallerEarly_MarksTheEscapingCancellation()
    {
        using var caller = new CancellationTokenSource();
        var tlsProvider = new StallingTlsProvider { OnStalled = caller.Cancel };
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider, new ManualTimeProvider(), connectTimeout: OneSecond);

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 29990, UseTls: true), caller.Token));

        Assert.IsTrue(exception.Data.Contains(ConnectResult.TcpConnectedOnCancellationKey));
        Assert.AreEqual(new IPEndPoint(Loopback, 29990), ConnectResult.CarriedBy(exception)?.RemoteEndPointBeforeFailure);
    }
}
