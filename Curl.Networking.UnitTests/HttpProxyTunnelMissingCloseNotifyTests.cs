using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// An HTTPS proxy that ends the connection during CONNECT with no reply and no TLS
/// close_notify is exit 56 with the schannel message (BL-5092).
/// </summary>
[TestClass]
public sealed class HttpProxyTunnelMissingCloseNotifyTests
{
    private const string AbruptMessage = "schannel: server closed abruptly (missing close_notify)";

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheTlsEndsWithoutCloseNotifyBeforeAnyByte_FailsWithExit56AndTheMessage()
    {
        var connection = new ScriptedConnection([]) { ReadException = new MissingCloseNotifyException(AbruptMessage) };

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, result.FailureExitCode);
        Assert.AreEqual(AbruptMessage, result.FailureMessage);
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheTlsEndsWithoutCloseNotifyAfterPartOfTheHead_ReturnsProxyConnectAborted()
    {
        var connection = new ScriptedConnection("HTTP/1.1 2"u8.ToArray()) { ExceptionAfterScript = new MissingCloseNotifyException(AbruptMessage) };

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, result.FailureExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.FailureMessage);
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenAPlainProxyClosesWithNoReply_StillFailsWithProxyConnectAborted()
    {
        var connection = new ScriptedConnection([]);

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.RecvError, result.FailureExitCode);
        Assert.AreEqual("Proxy CONNECT aborted", result.FailureMessage);
    }

    [TestMethod]
    public async Task ReadReplyAsync_WhenTheReadFailsWithASocketError_StillReturnsTheRecvFailure()
    {
        var failure = new IOException("x", new SocketException((int)SocketError.ConnectionReset));
        var connection = new ScriptedConnection([]) { ReadException = failure };

        var result = await HttpProxyTunnel.ReadReplyAsync(connection, CancellationToken.None);

        Assert.AreEqual(CurlSocketErrorText.ReceiveFailure(failure), result.FailureMessage);
    }
}
