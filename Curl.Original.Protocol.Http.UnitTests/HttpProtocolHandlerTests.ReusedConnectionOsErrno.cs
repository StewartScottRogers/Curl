using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http.Fakes;

namespace Curl.Original.Protocol.Http;

public sealed partial class HttpProtocolHandlerTests
{
    [TestMethod]
    public async Task ExecuteAsync_ReusedConnectionResetBeforeTheResponseAndFreshRetrySucceeds_KeepsTheResetAsOsErrno()
    {
        // libcurl 8.22.0 (BL-3315 differential, reuse-idle-close): CURLINFO_OS_ERRNO is the
        // dead connection's receive error, 10054 on Windows, although the retry answered 200.
        SocketException socketError = new((int)SocketError.ConnectionReset);
        ScriptedConnection dead = new([], 65536, failureAfterResponse: new IOException("Connection reset.", socketError));
        ScriptedConnection fresh = Connection("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nhi", 65536);
        QueueConnector connector = new(
            ConnectResult.Connected(dead, null, isReused: true, connectionNumber: 0),
            ConnectResult.Connected(fresh, null, connectionNumber: 1));
        Diagnostics.Arrange("socket error", socketError.SocketErrorCode);

        TransferResult result = await Handler(connector).ExecuteAsync(ReuseContext(new RecordingTransferEvents()));

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Diagnostics.Assert("os errno", socketError.NativeErrorCode, result.Report?.OsErrno);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual((long)socketError.NativeErrorCode, result.Report?.OsErrno);
    }
}
