using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

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
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Url = "pop3://127.0.0.1:18110/1";

    private const string Greeting = "+OK POP3 ready\r\n";

    private const string CapaReply = "+OK\r\nUSER\r\n.\r\n";

    private const string Ok = "+OK\r\n";

    private const string RetrReply = "+OK 3 octets\r\nhi\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string UserAndPass = "USER user\r\nPASS secret\r\n";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(0, "", 1, DisplayName = "CAPA")]
    [DataRow(1, Capa, 2, DisplayName = "USER")]
    [DataRow(3, Capa + UserAndPass, 4, DisplayName = "RETR")]
    public async Task ExecuteAsync_WriteResetOnWindows_FailsWithExit55WinsockWordsAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, Reset());

        TransferResult result = await ExecuteAsync(connection, new RecordingTransferEvents());

        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was reset"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was reset"), result);
        Diagnostics.AssertValues("Encoding.Latin1.GetString(connection.Sent)", sent, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(sent, Encoding.Latin1.GetString(connection.Sent));
        Diagnostics.AssertValues("connection.ReadCount", reads, connection.ReadCount);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(0, "", 1, DisplayName = "CAPA")]
    [DataRow(1, Capa, 2, DisplayName = "USER")]
    [DataRow(3, Capa + UserAndPass, 4, DisplayName = "RETR")]
    public async Task ExecuteAsync_WriteResetOffWindows_FailsWithExit55StrerrorWordsAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, Reset());

        TransferResult result = await ExecuteAsync(connection, new RecordingTransferEvents());

        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.SendError, "Send failure: " + ResetMessage()), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Send failure: " + ResetMessage()), result);
        Diagnostics.AssertValues("Encoding.Latin1.GetString(connection.Sent)", sent, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(sent, Encoding.Latin1.GetString(connection.Sent));
        Diagnostics.AssertValues("connection.ReadCount", reads, connection.ReadCount);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_WriteAbortedOnWindows_FailsWithExit55ConnectionWasAborted()
    {
        TransferResult result = await ExecuteAsync(Conversation(1, Aborted()), new RecordingTransferEvents());

        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was aborted"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Send failure: Connection was aborted"), result);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_WriteAbortedOffWindows_FailsWithExit55TheSocketErrorsOwnMessage()
    {
        TransferResult result = await ExecuteAsync(Conversation(1, Aborted()), new RecordingTransferEvents());

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.SendError, "Send failure: " + new SocketException((int)SocketError.ConnectionAborted).Message),
            result);
    }

    [TestMethod]
    [DataRow(0, "", 1, DisplayName = "CAPA")]
    [DataRow(1, Capa, 2, DisplayName = "USER")]
    [DataRow(3, Capa + UserAndPass, 4, DisplayName = "RETR")]
    public async Task ExecuteAsync_WriteFailsOtherwise_FailsWithExit55FailedSendingAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, new IOException("The pipe is broken."));

        TransferResult result = await ExecuteAsync(connection, new RecordingTransferEvents());

        Diagnostics.AssertValues("result", TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), result);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.SendError, "Failed sending data to the peer"), result);
        Diagnostics.AssertValues("Encoding.Latin1.GetString(connection.Sent)", sent, Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(sent, Encoding.Latin1.GetString(connection.Sent));
        Diagnostics.AssertValues("connection.ReadCount", reads, connection.ReadCount);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_RetrWriteResetOnWindows_ReportsTheFailureAndClosesTheConnection()
    {
        var events = new RecordingTransferEvents();

        await ExecuteAsync(Conversation(3, Reset()), events);

        CollectionAssert.AreEqual(
            (string[])["* Send failure: Connection was reset", "* closing connection #0"],
            events.Transcript[^2..]);
        Diagnostics.AssertValues("events.Transcript[^4]", "> PASS secret\r\n", events.Transcript[^4]);
        Assert.AreEqual("> PASS secret\r\n", events.Transcript[^4]);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_RetrWriteResetOffWindows_ReportsTheFailureAndClosesTheConnection()
    {
        var events = new RecordingTransferEvents();

        await ExecuteAsync(Conversation(3, Reset()), events);

        CollectionAssert.AreEqual(
            (string[])["* Send failure: " + ResetMessage(), "* closing connection #0"],
            events.Transcript[^2..]);
        Diagnostics.AssertValues("events.Transcript[^4]", "> PASS secret\r\n", events.Transcript[^4]);
        Assert.AreEqual("> PASS secret\r\n", events.Transcript[^4]);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFailsOtherwise_ReportsOnlyTheClosingLine()
    {
        var events = new RecordingTransferEvents();

        await ExecuteAsync(Conversation(1, new IOException("The pipe is broken.")), events);

        Diagnostics.AssertValues("events.Transcript[^1]", "* closing connection #0", events.Transcript[^1]);
        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
        Diagnostics.AssertValues("events.Transcript[^2]", "< .\r\n", events.Transcript[^2]);
        Assert.AreEqual("< .\r\n", events.Transcript[^2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitWriteFails_IgnoresItAsCurlDoes()
    {
        ScriptedConnection connection = Conversation(4, Reset());

        TransferResult result = await ExecuteAsync(connection, new RecordingTransferEvents());

        Diagnostics.AssertValues("result", TransferResult.Success(4), result);
        Assert.AreEqual(TransferResult.Success(4), result);
        Diagnostics.AssertValues("Encoding.Latin1.GetString(connection.Sent)", Capa + UserAndPass + "RETR 1\r\n", Encoding.Latin1.GetString(connection.Sent));
        Assert.AreEqual(Capa + UserAndPass + "RETR 1\r\n", Encoding.Latin1.GetString(connection.Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        ScriptedConnection connection = Conversation(1, new OperationCanceledException(cancellation.Token));

        OperationCanceledException thrown = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => ExecuteAsync(connection, new RecordingTransferEvents(), cancellation.Token).AsTask());
        Diagnostics.Act("thrown", $"{thrown.GetType().Name}: {thrown.Message}");
        Diagnostics.AssertValues("thrown for the cancelled token", true, thrown.CancellationToken == cancellation.Token);
    }

    private static IOException Reset() =>
        new("Unable to write data to the transport connection.", new SocketException((int)SocketError.ConnectionReset));

    private static string ResetMessage() => new SocketException((int)SocketError.ConnectionReset).Message;

    private static IOException Aborted() =>
        new("Unable to write data to the transport connection.", new SocketException((int)SocketError.ConnectionAborted));

    /// <summary>A whole USER/PASS retrieval, one response per read, whose writes fail from the given one on.</summary>
    private static ScriptedConnection Conversation(int writesBeforeFailure, Exception failure) =>
        new([.. new[] { Greeting, CapaReply, Ok, Ok, RetrReply, Bye }.Select(Encoding.Latin1.GetBytes)])
        {
            WritesBeforeFailure = writesBeforeFailure,
            WriteFailure = failure,
        };

    private async ValueTask<TransferResult> ExecuteAsync(ScriptedConnection connection, RecordingTransferEvents events, CancellationToken cancellationToken = default)
    {
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(Url),
            Output = Stream.Null,
            Events = events,
            Credentials = new NetworkCredential("user", "secret"),
            CancellationToken = cancellationToken,
        };
        Diagnostics.ArrangeRun(Url, connection.Script);
        Diagnostics.Arrange("writes before failure", $"{connection.WritesBeforeFailure}, then {connection.WriteFailure.GetType().Name}: {connection.WriteFailure.Message} ({connection.WriteFailure.InnerException?.Message ?? "no inner exception"})");
        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider()).ExecuteAsync(context);
        Diagnostics.ActTransfer(result, events, connection.Sent);
        return result;
    }
}
