using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// BL-5382: a SOCKS proxy that resets the connection during the handshake is exit 97
/// <c>Recv failure: ...</c>, as curl words it, and a proxy that closes cleanly keeps exit 97
/// <c>Failed to receive SOCKS response, proxy closed connection</c>.
/// </summary>
[TestClass]
public sealed class SocksProxyResetDuringHandshakeTests
{
    private static readonly byte[] GreetingReply = [5, 0];

    private static IOException Reset() =>
        new("reset", new SocketException((int)SocketError.ConnectionReset));

    private static async Task<ConnectResult> ConnectAsync(ProxyKind kind, ScriptedConnection connection)
    {
        var connector = new TcpConnector(
            new FakeDnsResolver(System.Net.IPAddress.Parse("192.0.2.10")),
            new FakeTcpDialer { DialOutcome = _ => connection },
            new FakeTlsProvider(),
            new ManualTimeProvider());

        return await connector.ConnectAsync(
            new ConnectTarget("example.test", 80, UseTls: false) { Proxy = new ProxyEndpoint(kind, "proxy.example", 1080, null) },
            CancellationToken.None);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks5)]
    [DataRow(ProxyKind.Socks5Hostname)]
    public async Task ConnectAsync_Socks5ResetWhileReadingConnectReply_IsExit97RecvFailure(ProxyKind kind)
    {
        var failure = Reset();
        var result = await ConnectAsync(kind, new ScriptedConnection(GreetingReply) { ExceptionAfterScript = failure });

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure), result.ErrorMessage);
        Assert.StartsWith("Recv failure: ", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks5)]
    [DataRow(ProxyKind.Socks5Hostname)]
    public async Task ConnectAsync_Socks5ResetWhileReadingGreetingReply_IsExit97RecvFailure(ProxyKind kind)
    {
        var failure = Reset();
        var result = await ConnectAsync(kind, new ScriptedConnection([]) { ExceptionAfterScript = failure });

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure), result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks4)]
    [DataRow(ProxyKind.Socks4a)]
    public async Task ConnectAsync_Socks4Reset_IsExit97RecvFailure(ProxyKind kind)
    {
        var failure = Reset();
        var result = await ConnectAsync(kind, new ScriptedConnection([]) { ExceptionAfterScript = failure });

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure), result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_Socks5ResetWhileWritingGreeting_IsExit97SendFailure()
    {
        var result = await ConnectAsync(ProxyKind.Socks5, new ScriptedConnection([]) { ExceptionOnWrite = Reset() });

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual("Unable to send initial SOCKS5 request.", result.ErrorMessage);
        Assert.AreEqual(CurlProxyCode.SendRequest, result.ProxyError);
    }

    [TestMethod]
    public async Task ConnectAsync_Socks4ResetWhileWritingRequest_IsExit97SendConnect()
    {
        var result = await ConnectAsync(ProxyKind.Socks4, new ScriptedConnection([]) { ExceptionOnWrite = Reset() });

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual("Failed to send SOCKS4 connect request.", result.ErrorMessage);
        Assert.AreEqual(CurlProxyCode.SendConnect, result.ProxyError);
    }

    [TestMethod]
    public async Task ConnectAsync_Socks5ResetWhileWritingConnectRequest_IsExit97SendConnect()
    {
        var result = await ConnectAsync(ProxyKind.Socks5, new ScriptedConnection(GreetingReply) { ExceptionOnWrite = Reset(), BytesWrittenBeforeException = 1 });

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual("Failed to send SOCKS5 connect request.", result.ErrorMessage);
        Assert.AreEqual(CurlProxyCode.SendConnect, result.ProxyError);
    }

    [TestMethod]
    public async Task ConnectAsync_WriteFailsWithoutSocketError_IsNotASendFailure()
    {
        var failure = new IOException("disk");
        var thrown = await Assert.ThrowsExactlyAsync<IOException>(
            () => ConnectAsync(ProxyKind.Socks5, new ScriptedConnection([]) { ExceptionOnWrite = failure }));

        Assert.AreSame(failure, thrown);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ConnectAsync_ResetOnWindows_SaysConnectionWasReset()
    {
        var result = await ConnectAsync(ProxyKind.Socks5, new ScriptedConnection(GreetingReply) { ExceptionAfterScript = Reset() });

        Assert.AreEqual("Recv failure: Connection was reset", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(ProxyKind.Socks5)]
    [DataRow(ProxyKind.Socks4)]
    public async Task ConnectAsync_CleanCloseDuringHandshake_KeepsProxyClosedMessage(ProxyKind kind)
    {
        var result = await ConnectAsync(kind, new ScriptedConnection(kind == ProxyKind.Socks5 ? GreetingReply : []));

        Assert.AreEqual(CurlExitCode.Proxy, result.ExitCode);
        Assert.AreEqual(SocksProxyTunnel.ProxyClosedMessage, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ConnectAsync_NonSocketIoFailure_Escapes()
    {
        var connection = new ScriptedConnection([]) { ExceptionAfterScript = new IOException("plain") };

        await Assert.ThrowsExactlyAsync<IOException>(() => ConnectAsync(ProxyKind.Socks5, connection));
    }
}
