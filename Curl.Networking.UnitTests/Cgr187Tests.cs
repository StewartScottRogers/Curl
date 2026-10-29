using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-187 of the third-party gap report (BL-3716): <c>--connect-timeout 0.01</c> to a loopback
/// address connects in curl, whose non-blocking connect to loopback completes before its timer is
/// first checked, while here the runtime's own overhead before the dial finishes can pass 10 ms
/// and cancel it. A loopback destination is therefore held to at least
/// <see cref="TcpConnector.MinimumLoopbackConnectTimeout" />.
/// </summary>
[TestClass]
public sealed class Cgr187Tests
{
    [TestMethod]
    public async Task ConnectAsync_ToLoopbackWithATenMillisecondLimitPassedDuringTheDial_StillConnects()
    {
        var time = new ManualTimeProvider();
        var dialer = new TokenCheckingTcpDialer(() => time.Advance(50));
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: TimeSpan.FromMilliseconds(10));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 80, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_ToLoopbackStalledPastTheMinimum_StillTimesOut()
    {
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => time.Advance(150) };
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: TimeSpan.FromMilliseconds(10));

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_ToANonLoopbackAddressWithATenMillisecondLimit_TimesOutAtTenMilliseconds()
    {
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => time.Advance(11) };
        var connector = new TcpConnector(new FakeDnsResolver(IPAddress.Parse("10.255.255.1")), dialer, new FakeTlsProvider(), time, connectTimeout: TimeSpan.FromMilliseconds(10));

        var result = await connector.ConnectAsync(new ConnectTarget("10.255.255.1", 80, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 11 milliseconds", result.ErrorMessage);
    }

    private sealed class TokenCheckingTcpDialer(Action beforeChecking) : ITcpDialer
    {
        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            beforeChecking();
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(new DialedTcpConnection(new FakeConnection(), new IPEndPoint(IPAddress.Loopback, 50000)));
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
