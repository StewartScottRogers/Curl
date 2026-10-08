using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Http.Fakes;

namespace Curl.Protocol.Http;

/// <summary>
/// A read that finds the TLS connection ended without <c>close_notify</c>: the connection
/// throws <see cref="MissingCloseNotifyException" /> and the transfer fails with exit 56 and
/// the exception's text, whatever the body's framing, keeping the bytes already written -
/// as curl 8.21.0 (mingw, Schannel) and curl 8.18.0 (Ubuntu, OpenSSL 3.5.5) did against
/// <c>Record-CurlExchange.ps1 -Tls</c> on 2026-09-29 (ADR-0221).
/// </summary>
public sealed partial class HttpProtocolHandlerTests
{
    private const string SchannelMissingCloseNotify = "schannel: server closed abruptly (missing close_notify)";

    private const string OpenSslMissingCloseNotify =
        "OpenSSL SSL_read: OpenSSL/3.5.7: error:0A000126:SSL routines::unexpected eof while reading, errno 0";

    [TestMethod]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nhello", SchannelMissingCloseNotify, DisplayName = "Read until close, Schannel")]
    [DataRow("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nhello", OpenSslMissingCloseNotify, DisplayName = "Read until close, OpenSSL")]
    [DataRow("HTTP/1.1 200 OK\r\nContent-Length: 10\r\n\r\nhello", SchannelMissingCloseNotify, DisplayName = "Content-Length short")]
    [DataRow("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n", OpenSslMissingCloseNotify, DisplayName = "Chunked unfinished")]
    public async Task ExecuteAsync_ConnectionEndsWithoutCloseNotify_FailsWithExit56AndTheBuildsText(string response, string message)
    {
        var output = new MemoryStream();
        var connection = new ScriptedConnection(Encoding.Latin1.GetBytes(response), 65536, failureAfterResponse: new MissingCloseNotifyException(message));
        Diagnostics.Arrange("scripted response", OneLine(response));
        Diagnostics.Arrange("failure after response", message);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(Context("http://127.0.0.1:48191/", output));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Assert("error message", message, result.ErrorMessage);
        Assert.AreEqual(message, result.ErrorMessage);
        Diagnostics.Assert("output", "hello", Latin1(output.ToArray()));
        Assert.AreEqual("hello", Latin1(output.ToArray()));
    }

    [TestMethod]
    public async Task ExecuteAsync_ConnectionEndsWithoutCloseNotifyBeforeTheHead_FailsWithExit56AndTheBuildsText()
    {
        var connection = new ScriptedConnection([], 65536, failureAfterResponse: new MissingCloseNotifyException(SchannelMissingCloseNotify));
        Diagnostics.Arrange("scripted response", "(empty)");
        Diagnostics.Arrange("failure after response", SchannelMissingCloseNotify);

        TransferResult result = await Handler(QueueConnector.For(connection))
            .ExecuteAsync(Context("http://127.0.0.1:48191/", new MemoryStream()));

        WriteResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.RecvError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.RecvError, result.ExitCode);
        Diagnostics.Assert("error message", SchannelMissingCloseNotify, result.ErrorMessage);
        Assert.AreEqual(SchannelMissingCloseNotify, result.ErrorMessage);
    }
}
