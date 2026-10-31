using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins third-party gap report issue CGR-006 (BL-3535): an FTPS upload ends its data
/// connection with <c>close_notify</c> and FIN, then reads what the server still sends (a
/// TLS 1.3 session ticket) until the server closes, for at most two seconds, before it is
/// disposed. Disposing it with the ticket unread made Windows reset the connection, and the
/// reset threw away the upload the server had not read yet (ADR-0891).
/// </summary>
[TestClass]
public sealed class Cgr006Tests
{
    private const string Replies =
        "220 ready\r\n331 Password required\r\n230 Logged in\r\n200 PBSZ=0\r\n200 Protection level set to P\r\n257 \"/\" is current directory\r\n" +
        "229 Entering Extended Passive Mode (|||64396|)\r\n200 Type set\r\n150 Ok to send data\r\n226 Transfer complete\r\n221 Bye\r\n";

    [TestMethod]
    public async Task ExecuteAsync_FtpsUpload_ShutsDownThenReadsTheServersLastBytesBeforeDisposing()
    {
        var log = new List<string>();
        var data = new CloseRecordingConnection(log, new ScriptedConnection(Encoding.Latin1.GetBytes("ticket")));

        TransferResult result = await RunAsync(data, TimeProvider.System);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "write up1\nup2\n", "shutdown", "read 6", "read 0", "dispose" }, log);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsUploadServerResetsAfterTheShutdown_StillDisposesAndSucceeds()
    {
        var log = new List<string>();
        var data = new CloseRecordingConnection(log, new ScriptedConnection { FailReadsWhenExhausted = true });

        TransferResult result = await RunAsync(data, TimeProvider.System);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "write up1\nup2\n", "shutdown", "read failed", "dispose" }, log);
    }

    [TestMethod]
    public async Task ExecuteAsync_FtpsUploadServerKeepsTheDataOpen_StopsReadingAfterTwoSecondsAndSucceeds()
    {
        var log = new List<string>();
        var data = new CloseRecordingConnection(log, new StallingConnection());
        var time = new ImmediateTimerTimeProvider();

        TransferResult result = await RunAsync(data, time);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "write up1\nup2\n", "shutdown", "read failed", "dispose" }, log);
        CollectionAssert.Contains(time.Waits, TimeSpan.FromSeconds(2));
    }

    private static async Task<TransferResult> RunAsync(CloseRecordingConnection data, TimeProvider time)
    {
        var connector = new QueuedConnector(
            ConnectResult.Connected(ScriptedConnection.FromReplies(Replies)),
            ConnectResult.Connected(new ScriptedConnection()));
        var tls = new QueuedTlsProvider(ConnectResult.Connected(data));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ftps://127.0.0.1/u.txt"),
            Output = new MemoryStream(),
            Upload = new MemoryStream(Encoding.Latin1.GetBytes("up1\nup2\n")),
            TimeProvider = time,
        };

        return await new FtpProtocolHandler(connector, new QueuedListener(), tls).ExecuteAsync(context);
    }

    /// <summary>A data connection that logs its writes, shutdown, reads and disposal in order.</summary>
    private sealed class CloseRecordingConnection(List<string> log, IConnection inner) : IConnection
    {
        public bool IsSecure => true;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            try
            {
                int read = await inner.ReadAsync(buffer, cancellationToken);
                log.Add("read " + read);
                return read;
            }
            catch (Exception)
            {
                log.Add("read failed");
                throw;
            }
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            log.Add("write " + Encoding.Latin1.GetString(buffer.Span));
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken)
        {
            log.Add("shutdown");
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            log.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }
}
