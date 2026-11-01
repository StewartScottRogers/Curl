using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ftp.Fakes;

namespace Curl.Original.Protocol.Ftp;

/// <summary>
/// Pins that the data connection's send side is ended gracefully (<see cref="IConnection.ShutDownSendingAsync" />:
/// TLS <c>close_notify</c>, then FIN) before it is disposed, and both before the end-of-transfer
/// reply is read, so a server such as Rebex does not see a reset and answer <c>426</c>
/// (BL-3179, ADR-0770); and that a shutdown failing because the peer has gone still closes the
/// connection and reads the reply.
/// </summary>
[TestClass]
public sealed class FtpProtocolHandlerDataShutdownTests
{
    private const string BeforeTransferEnd = "220 Recorder ready\r\n331 Password required\r\n230 Logged in\r\n257 \"/\" is current directory\r\n"
        + "229 Entering Extended Passive Mode (|||61744|)\r\n200 Type set\r\n213 5\r\n150 Opening BINARY mode data connection\r\n";

    [TestMethod]
    public async Task ExecuteAsync_Download_ShutsTheDataSendSideDownThenDisposesItBeforeTheEndReply()
    {
        var log = new List<string>();
        var data = new ShutDownRecordingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes("hello")), log);

        TransferResult result = await RunAsync(new EndReplyLoggingControl(log), data);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "shut down", "disposed", "end-of-transfer reply" }, log);
    }

    [TestMethod]
    public async Task ExecuteAsync_DataShutdownFailsWithIOException_StillDisposesAndSucceeds()
    {
        var log = new List<string>();
        var data = new ShutDownRecordingConnection(new ScriptedConnection(Encoding.Latin1.GetBytes("hello")), log) { ShutDownFails = true };

        TransferResult result = await RunAsync(new EndReplyLoggingControl(log), data);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "shut down", "disposed", "end-of-transfer reply" }, log);
    }

    private static async Task<TransferResult> RunAsync(IConnection control, IConnection data)
    {
        var connector = new QueuedConnector(ConnectResult.Connected(control), ConnectResult.Connected(data));
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ftp://127.0.0.1:18321/file.txt"),
            Output = new MemoryStream(),
            TimeProvider = new SteppingTimeProvider(),
        };

        return await new FtpProtocolHandler(connector).ExecuteAsync(context);
    }

    /// <summary>A data connection that logs its shutdown and its dispose.</summary>
    private sealed class ShutDownRecordingConnection(ScriptedConnection inner, List<string> log) : IConnection
    {
        public bool ShutDownFails { get; init; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => inner.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken)
        {
            log.Add("shut down");
            return ShutDownFails ? ValueTask.FromException(new IOException("The connection was reset.")) : ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            log.Add("disposed");
            return inner.DisposeAsync();
        }
    }

    /// <summary>A control connection that logs when the end-of-transfer reply, <c>226</c>, is read.</summary>
    private sealed class EndReplyLoggingControl(List<string> log) : IConnection
    {
        private readonly ScriptedConnection before = ScriptedConnection.FromReplies(BeforeTransferEnd);

        private readonly ScriptedConnection after = ScriptedConnection.FromReplies("221 Bye\r\n");

        private bool endAnswered;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int read = await before.ReadAsync(buffer, cancellationToken);
            if (read > 0)
            {
                return read;
            }

            if (endAnswered)
            {
                return await after.ReadAsync(buffer, cancellationToken);
            }

            endAnswered = true;
            log.Add("end-of-transfer reply");
            byte[] reply = "226 Transfer complete\r\n"u8.ToArray();
            reply.CopyTo(buffer);
            return reply.Length;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => before.WriteAsync(buffer, cancellationToken);

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
