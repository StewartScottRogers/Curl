using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP transfer ends when a command or the message cannot be written (BL-1243):
/// curl 8.21.0 ends it at once with exit 55, <c>Send failure: &lt;words&gt;</c> for any socket
/// error (the socket filter's <c>failf</c>, Winsock's words on Windows and <c>strerror</c>'s
/// elsewhere, BL-1345) and <c>Failed sending data to the peer</c> for a failure with no socket
/// error in it, and sends and reads nothing more, not even <c>QUIT</c>.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerSendFailureTests
{
    private const string Url = "smtp://127.0.0.1:18025/client";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 SMTPUTF8\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Accepted = "250 OK message accepted\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Ehlo = "EHLO client\r\n";

    private const string MailFrom = "MAIL FROM:<a@b>\r\n";

    private const string Envelope = MailFrom + "RCPT TO:<c@d>\r\nDATA\r\n";

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(0, "", 1, DisplayName = "EHLO")]
    [DataRow(1, Ehlo, 2, DisplayName = "MAIL FROM")]
    [DataRow(4, Ehlo + Envelope, 5, DisplayName = "the message body")]
    public async Task ExecuteAsync_WriteResetOnWindows_FailsWithExit55WinsockWordsAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, Reset());

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(new RecordingTransferEvents()), connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Send failure: Connection was reset", run.Result.ErrorMessage);
        Assert.AreEqual("Send failure: Connection was reset", run.Result.ErrorMessage);
        Diagnostics.Diff("sent", sent, run.Sent);
        Diagnostics.Assert("reads", reads, connection.ReadCount);
        Assert.AreEqual(sent, run.Sent);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(0, "", 1, DisplayName = "EHLO")]
    [DataRow(1, Ehlo, 2, DisplayName = "MAIL FROM")]
    [DataRow(4, Ehlo + Envelope, 5, DisplayName = "the message body")]
    public async Task ExecuteAsync_WriteResetOffWindows_FailsWithExit55StrerrorWordsAndStops(int writesBeforeFailure, string sent, int reads)
    {
        IOException failure = Reset();
        ScriptedConnection connection = Conversation(writesBeforeFailure, failure);

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(new RecordingTransferEvents()), connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Send failure: " + failure.InnerException!.Message, run.Result.ErrorMessage);
        Assert.AreEqual("Send failure: " + failure.InnerException!.Message, run.Result.ErrorMessage);
        Diagnostics.Diff("sent", sent, run.Sent);
        Diagnostics.Assert("reads", reads, connection.ReadCount);
        Assert.AreEqual(sent, run.Sent);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(1, Ehlo, 2, DisplayName = "MAIL FROM")]
    [DataRow(4, Ehlo + Envelope, 5, DisplayName = "the message body")]
    public async Task ExecuteAsync_WriteAbortedOnWindows_FailsWithExit55ConnectionWasAborted(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, Aborted());

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(new RecordingTransferEvents()), connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Send failure: Connection was aborted", run.Result.ErrorMessage);
        Assert.AreEqual("Send failure: Connection was aborted", run.Result.ErrorMessage);
        Diagnostics.Diff("sent", sent, run.Sent);
        Diagnostics.Assert("reads", reads, connection.ReadCount);
        Assert.AreEqual(sent, run.Sent);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    [DataRow(1, Ehlo, 2, DisplayName = "MAIL FROM")]
    [DataRow(4, Ehlo + Envelope, 5, DisplayName = "the message body")]
    public async Task ExecuteAsync_WriteAbortedOffWindows_FailsWithExit55StrerrorWords(int writesBeforeFailure, string sent, int reads)
    {
        IOException failure = Aborted();
        ScriptedConnection connection = Conversation(writesBeforeFailure, failure);

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(new RecordingTransferEvents()), connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Send failure: " + failure.InnerException!.Message, run.Result.ErrorMessage);
        Assert.AreEqual("Send failure: " + failure.InnerException!.Message, run.Result.ErrorMessage);
        Diagnostics.Diff("sent", sent, run.Sent);
        Diagnostics.Assert("reads", reads, connection.ReadCount);
        Assert.AreEqual(sent, run.Sent);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [DataRow(0, "", 1, DisplayName = "EHLO")]
    [DataRow(1, Ehlo, 2, DisplayName = "MAIL FROM")]
    [DataRow(4, Ehlo + Envelope, 5, DisplayName = "the message body")]
    public async Task ExecuteAsync_WriteFailsOtherwise_FailsWithExit55FailedSendingAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, new IOException("The pipe is broken."));

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(new RecordingTransferEvents()), connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Failed sending data to the peer", run.Result.ErrorMessage);
        Assert.AreEqual("Failed sending data to the peer", run.Result.ErrorMessage);
        Diagnostics.Diff("sent", sent, run.Sent);
        Diagnostics.Assert("reads", reads, connection.ReadCount);
        Assert.AreEqual(sent, run.Sent);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandWriteResetOnWindows_ReportsTheFailureAndClosesTheConnection()
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(events), Conversation(1, Reset()));
        Diagnostics.ActEvents(run.Result, events);

        string[] expected = [
            "< 220 localhost ESMTP\r\n", "> EHLO client\r\n", "< 250-localhost\r\n", "< 250 SMTPUTF8\r\n",
            "* Send failure: Connection was reset", "* closing connection #0",
        ];
        AssertTranscript(expected, events.Transcript);
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandWriteResetOffWindows_ReportsTheFailureAndClosesTheConnection()
    {
        RecordingTransferEvents events = new();
        IOException failure = Reset();

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(events), Conversation(1, failure));
        Diagnostics.ActEvents(run.Result, events);

        string[] expected = [
            "< 220 localhost ESMTP\r\n", "> EHLO client\r\n", "< 250-localhost\r\n", "< 250 SMTPUTF8\r\n",
            "* Send failure: " + failure.InnerException!.Message, "* closing connection #0",
        ];
        AssertTranscript(expected, events.Transcript);
        CollectionAssert.AreEqual(expected, events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFailsOtherwise_ReportsOnlyTheClosingLine()
    {
        RecordingTransferEvents events = new();

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(events), Conversation(1, new IOException("The pipe is broken.")));
        Diagnostics.ActEvents(run.Result, events);

        Diagnostics.AssertValues("last transcript line", "* closing connection #0", events.Transcript[^1]);
        Diagnostics.AssertValues("second to last transcript line", "< 250 SMTPUTF8\r\n", events.Transcript[^2]);
        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
        Assert.AreEqual("< 250 SMTPUTF8\r\n", events.Transcript[^2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitWriteFails_IgnoresItAsCurlDoes()
    {
        ScriptedConnection connection = Conversation(5, Reset());

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, MailContext(new RecordingTransferEvents()), connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.Ok, run.Result.ExitCode);
        Diagnostics.Diff("sent", Ehlo + Envelope + "one\r\n.\r\n", run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(Ehlo + Envelope + "one\r\n.\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandsQuitWriteFails_IgnoresItAsCurlDoes()
    {
        var connection = new ScriptedConnection(Bytes(Greeting), Bytes(EhloReply), Bytes(SmtpRun.HelpReply)) { WritesBeforeFailure = 2, WriteFailure = Reset() };

        Diagnostics.Arrange("writes before failure", 2);

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, Url, connection);

        Diagnostics.AssertResult(SmtpRun.HelpAnswered, run.Result);
        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandsWriteResetOnWindows_FailsWithExit55()
    {
        var connection = new ScriptedConnection(Bytes(Greeting), Bytes(EhloReply), Bytes(SmtpRun.HelpReply)) { WritesBeforeFailure = 1, WriteFailure = Reset() };

        Diagnostics.Arrange("writes before failure", 1);

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, Url, connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Send failure: Connection was reset", run.Result.ErrorMessage);
        Diagnostics.Diff("sent", Ehlo, run.Sent);
        Diagnostics.Assert("reads", 2, connection.ReadCount);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", run.Result.ErrorMessage);
        Assert.AreEqual(Ehlo, run.Sent);
        Assert.AreEqual(2, connection.ReadCount);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task ExecuteAsync_CommandsWriteResetOffWindows_FailsWithExit55()
    {
        IOException failure = Reset();
        var connection = new ScriptedConnection(Bytes(Greeting), Bytes(EhloReply), Bytes(SmtpRun.HelpReply)) { WritesBeforeFailure = 1, WriteFailure = failure };

        Diagnostics.Arrange("writes before failure", 1);

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, Url, connection);

        Diagnostics.AssertValues("exit code", CurlExitCode.SendError, run.Result.ExitCode);
        Diagnostics.AssertValues("error message", "Send failure: " + failure.InnerException!.Message, run.Result.ErrorMessage);
        Diagnostics.Diff("sent", Ehlo, run.Sent);
        Diagnostics.Assert("reads", 2, connection.ReadCount);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("Send failure: " + failure.InnerException!.Message, run.Result.ErrorMessage);
        Assert.AreEqual(Ehlo, run.Sent);
        Assert.AreEqual(2, connection.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        TransferContext context = MailContext(new RecordingTransferEvents(), cancellation.Token);
        Diagnostics.ArrangeContext(context, "(the conversation's replies)");
        Diagnostics.Arrange("cancellation token", "already cancelled; the second write throws OperationCanceledException");
        Diagnostics.Act("ExecuteAsync", "awaited below");
        Diagnostics.Assert("exception type", nameof(OperationCanceledException), "OperationCanceledException expected from the awaited call");

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => SmtpRun.ExecuteAsync(context, Conversation(1, new OperationCanceledException(cancellation.Token))));
    }

    private static string Join(IEnumerable<string> lines) => string.Join(" | ", lines.Select(SmtpDiagnostics.Show));

    private void AssertTranscript(string[] expected, IEnumerable<string> actual) =>
        Diagnostics.Assert("transcript", Join(expected), Join(actual));

    private static IOException Reset() =>
        new("Unable to write data to the transport connection.", new SocketException((int)SocketError.ConnectionReset));

    private static IOException Aborted() =>
        new("Unable to write data to the transport connection.", new SocketException((int)SocketError.ConnectionAborted));

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    /// <summary>A whole upload's conversation, one reply per read, whose writes fail from the given one on.</summary>
    private static ScriptedConnection Conversation(int writesBeforeFailure, Exception failure) =>
        new(Bytes(Greeting), Bytes(EhloReply), Bytes(Ok), Bytes(Ok), Bytes(StartData), Bytes(Accepted), Bytes(Bye))
        {
            WritesBeforeFailure = writesBeforeFailure,
            WriteFailure = failure,
        };

    private static TransferContext MailContext(RecordingTransferEvents events, CancellationToken cancellationToken = default) => new()
    {
        Url = CurlUrl.Parse(Url),
        Output = Stream.Null,
        Events = events,
        Upload = new MemoryStream("one\r\n"u8.ToArray()),
        Mail = new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
        CancellationToken = cancellationToken,
    };
}
