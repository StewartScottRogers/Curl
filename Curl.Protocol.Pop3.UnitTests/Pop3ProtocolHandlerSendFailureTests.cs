using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins how a POP3 transfer ends when a command cannot be written (BL-1252): curl 8.21.0 ends
/// it at once with exit 55, <c>Send failure: Connection was reset</c> for a reset (the socket
/// filter's <c>failf</c>) and <c>Failed sending data to the peer</c> for any other failure, and
/// sends and reads nothing more, not even <c>QUIT</c>.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerSendFailureTests
{
    private const string Url = "pop3://127.0.0.1:18110/1";

    private const string Greeting = "+OK POP3 ready\r\n";

    private const string CapaReply = "+OK\r\nUSER\r\n.\r\n";

    private const string Ok = "+OK\r\n";

    private const string RetrReply = "+OK 3 octets\r\nhi\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string UserAndPass = "USER user\r\nPASS secret\r\n";

    [TestMethod]
    [DataRow(0, "", 1, DisplayName = "CAPA")]
    [DataRow(1, Capa, 2, DisplayName = "USER")]
    [DataRow(3, Capa + UserAndPass, 4, DisplayName = "RETR")]
    public async Task ExecuteAsync_WriteReset_FailsWithExit55SendFailureAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, Reset());

        TransferResult result = await ExecuteAsync(connection, new RecordingTransferEvents());

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was reset"), result);
        Assert.AreEqual(sent, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [DataRow(0, "", 1, DisplayName = "CAPA")]
    [DataRow(1, Capa, 2, DisplayName = "USER")]
    [DataRow(3, Capa + UserAndPass, 4, DisplayName = "RETR")]
    public async Task ExecuteAsync_WriteFailsOtherwise_FailsWithExit55FailedSendingAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, new IOException("The pipe is broken."));

        TransferResult result = await ExecuteAsync(connection, new RecordingTransferEvents());

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), result);
        Assert.AreEqual(sent, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_RetrWriteReset_ReportsTheFailureAndClosesTheConnection()
    {
        var events = new RecordingTransferEvents();

        await ExecuteAsync(Conversation(3, Reset()), events);

        CollectionAssert.AreEqual(
            (string[])["* Send failure: Connection was reset", "* closing connection #0"],
            events.Transcript[^2..]);
        Assert.AreEqual("> PASS secret\r\n", events.Transcript[^4]);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFailsOtherwise_ReportsOnlyTheClosingLine()
    {
        var events = new RecordingTransferEvents();

        await ExecuteAsync(Conversation(1, new IOException("The pipe is broken.")), events);

        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
        Assert.AreEqual("< .\r\n", events.Transcript[^2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitWriteFails_IgnoresItAsCurlDoes()
    {
        ScriptedConnection connection = Conversation(4, Reset());

        TransferResult result = await ExecuteAsync(connection, new RecordingTransferEvents());

        Assert.AreEqual(TransferResult.Success(4), result);
        Assert.AreEqual(Capa + UserAndPass + "RETR 1\r\n", Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        ScriptedConnection connection = Conversation(1, new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ExecuteAsync(connection, new RecordingTransferEvents(), cancellation.Token).AsTask());
    }

    private static IOException Reset() =>
        new("Unable to write data to the transport connection.", new SocketException((int)SocketError.ConnectionReset));

    /// <summary>A whole USER/PASS retrieval, one response per read, whose writes fail from the given one on.</summary>
    private static ScriptedConnection Conversation(int writesBeforeFailure, Exception failure) =>
        new([.. new[] { Greeting, CapaReply, Ok, Ok, RetrReply, Bye }.Select(Encoding.Latin1.GetBytes)])
        {
            WritesBeforeFailure = writesBeforeFailure,
            WriteFailure = failure,
        };

    private static ValueTask<TransferResult> ExecuteAsync(ScriptedConnection connection, RecordingTransferEvents events, CancellationToken cancellationToken = default)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Events = events,
            Credentials = new NetworkCredential("user", "secret"),
            CancellationToken = cancellationToken,
        };
        return new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider()).ExecuteAsync(context);
    }
}
