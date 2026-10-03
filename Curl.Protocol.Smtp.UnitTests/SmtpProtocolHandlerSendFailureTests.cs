using System.Net.Sockets;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Pins how an SMTP transfer ends when a command or the message cannot be written (BL-1243):
/// curl 8.21.0 ends it at once with exit 55, <c>Send failure: Connection was reset</c> for a
/// reset (the socket filter's <c>failf</c>) and <c>Failed sending data to the peer</c> for any
/// other failure, and sends and reads nothing more, not even <c>QUIT</c>.
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

    [TestMethod]
    [DataRow(0, "", 1, DisplayName = "EHLO")]
    [DataRow(1, Ehlo, 2, DisplayName = "MAIL FROM")]
    [DataRow(4, Ehlo + Envelope, 5, DisplayName = "the message body")]
    public async Task ExecuteAsync_WriteReset_FailsWithExit55SendFailureAndStops(int writesBeforeFailure, string sent, int reads)
    {
        ScriptedConnection connection = Conversation(writesBeforeFailure, Reset());

        SmtpRun run = await SmtpRun.ExecuteAsync(MailContext(new RecordingTransferEvents()), connection);

        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", run.Result.ErrorMessage);
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

        SmtpRun run = await SmtpRun.ExecuteAsync(MailContext(new RecordingTransferEvents()), connection);

        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("Failed sending data to the peer", run.Result.ErrorMessage);
        Assert.AreEqual(sent, run.Sent);
        Assert.AreEqual(reads, connection.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteReset_ReportsTheFailureAndClosesTheConnection()
    {
        RecordingTransferEvents events = new();

        await SmtpRun.ExecuteAsync(MailContext(events), Conversation(1, Reset()));

        CollectionAssert.AreEqual(
            (string[])[
                "< 220 localhost ESMTP\r\n", "> EHLO client\r\n", "< 250-localhost\r\n", "< 250 SMTPUTF8\r\n",
                "* Send failure: Connection was reset", "* closing connection #0",
            ],
            events.Transcript);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandWriteFailsOtherwise_ReportsOnlyTheClosingLine()
    {
        RecordingTransferEvents events = new();

        await SmtpRun.ExecuteAsync(MailContext(events), Conversation(1, new IOException("The pipe is broken.")));

        Assert.AreEqual("* closing connection #0", events.Transcript[^1]);
        Assert.AreEqual("< 250 SMTPUTF8\r\n", events.Transcript[^2]);
    }

    [TestMethod]
    public async Task ExecuteAsync_QuitWriteFails_IgnoresItAsCurlDoes()
    {
        ScriptedConnection connection = Conversation(5, Reset());

        SmtpRun run = await SmtpRun.ExecuteAsync(MailContext(new RecordingTransferEvents()), connection);

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(Ehlo + Envelope + "one\r\n.\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandsQuitWriteFails_IgnoresItAsCurlDoes()
    {
        var connection = new ScriptedConnection(Bytes(Greeting), Bytes(EhloReply), Bytes(SmtpRun.HelpReply)) { WritesBeforeFailure = 2, WriteFailure = Reset() };

        SmtpRun run = await SmtpRun.ExecuteAsync(Url, connection);

        Assert.AreEqual(SmtpRun.HelpAnswered, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_CommandsWriteReset_FailsWithExit55()
    {
        var connection = new ScriptedConnection(Bytes(Greeting), Bytes(EhloReply), Bytes(SmtpRun.HelpReply)) { WritesBeforeFailure = 1, WriteFailure = Reset() };

        SmtpRun run = await SmtpRun.ExecuteAsync(Url, connection);

        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("Send failure: Connection was reset", run.Result.ErrorMessage);
        Assert.AreEqual(Ehlo, run.Sent);
        Assert.AreEqual(2, connection.ReadCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_WriteCancelled_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        TransferContext context = MailContext(new RecordingTransferEvents(), cancellation.Token);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => SmtpRun.ExecuteAsync(context, Conversation(1, new OperationCanceledException(cancellation.Token))));
    }

    private static IOException Reset() =>
        new("Unable to write data to the transport connection.", new SocketException((int)SocketError.ConnectionReset));

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
