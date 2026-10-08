using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Smtp.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Smtp;

/// <summary>
/// Attacks <see cref="SmtpProtocolHandler" /> from its public surface by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1517): a message read and a reply
/// delivered one byte at a time, lines at and past 998 octets, reply lines at the 65536-byte
/// limit, CR and LF inside <c>--mail-from</c> and <c>--mail-rcpt</c>, out-of-range reply codes,
/// lines that carry no reply code, and many recipients. Where the answer shows on the command
/// line it is curl 8.21.0's (the Schannel build), measured on 2026-10-07 with
/// <c>Record-CurlExchange.ps1 -Smtp</c>.
/// </summary>
[TestClass]
public sealed class SmtpProtocolHandlerAdversarialTests
{
    private const string Url = "smtp://127.0.0.1:18025/dom";

    private const string Greeting = "220 localhost ESMTP\r\n";

    private const string EhloReply = "250-localhost\r\n250 8BITMIME\r\n";

    private const string Ok = "250 OK\r\n";

    private const string StartData = "354 End data with <CR><LF>.<CR><LF>\r\n";

    private const string Bye = "221 Bye\r\n";

    private const string Envelope = "EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nDATA\r\n";

    private const string Quit = "QUIT\r\n";

    private const string Accepting = Greeting + EhloReply + Ok + Ok + StartData + Ok + Bye;

    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("a\r\n.b\r\n.\r\n..\r\nc", DisplayName = "Dots after every CRLF")]
    [DataRow(".", DisplayName = "A lone dot")]
    [DataRow("x\r", DisplayName = "Ends in CR")]
    [DataRow("x\r\n", DisplayName = "Ends in CRLF")]
    [DataRow("a\r\r\n.b\n.c\r.d", DisplayName = "CR before CRLF, bare LF and bare CR")]
    public async Task ExecuteAsync_UploadReadOneByteAtATime_SendsTheSameBytesAsReadWhole(string body)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(body);
        SmtpRun whole = await RunAsync(Accepting, new MemoryStream(bytes));
        SmtpRun byteByByte = await RunAsync(Accepting, new OneByteReadStream(bytes));

        Diagnostics.Diff("sent", whole.Sent, byteByByte.Sent);
        Assert.AreEqual(whole.Sent, byteByByte.Sent);
        Assert.AreEqual(CurlExitCode.Ok, byteByByte.Result.ExitCode);
        Assert.AreEqual(whole.Result.BytesTransferred, byteByByte.Result.BytesTransferred);
    }

    [TestMethod]
    [DataRow(998, DisplayName = "998 octets, RFC 5321's limit")]
    [DataRow(999, DisplayName = "999 octets")]
    [DataRow(70000, DisplayName = "70000 octets")]
    public async Task ExecuteAsync_BodyLineAtAndPastTheRfcLimit_IsSentUnbrokenAsCurlDoes(int length)
    {
        string line = "." + new string('x', length - 1);
        SmtpRun run = await RunAsync(Accepting, Body(line + "\r\n"));

        string expected = Envelope + "." + line + "\r\n.\r\n" + Quit;
        Assert.AreEqual(expected, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(length + 6L, run.Result.BytesTransferred);
    }

    [TestMethod]
    public async Task ExecuteAsync_EveryReplyDeliveredOneByteAtATime_SendsTheMessageAsWhenDeliveredWhole()
    {
        byte[][] reads = [.. Encoding.Latin1.GetBytes(Accepting).Select(single => new[] { single })];
        var connection = new ScriptedConnection(reads);

        SmtpRun run = await SmtpRun.ExecuteAsync(Diagnostics, Context(Body("one\r\n")), connection);

        Assert.AreEqual(Envelope + "one\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(250, run.Result.Report?.ResponseCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailReplyLineOneByteUnderTheLimit_IsAccepted()
    {
        // 65535 bytes, CR and LF included: one under the line length curl refuses.
        string longOk = "250 " + new string('x', 65535 - 6) + "\r\n";
        SmtpRun run = await RunAsync(Greeting + EhloReply + longOk + Ok + StartData + Ok + Bye, Body("one\r\n"));

        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual(Envelope + "one\r\n.\r\n" + Quit, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailReplyLineAtTheLimit_IsExit100()
    {
        // 65536 bytes, CR and LF included: the shortest line curl refuses.
        string longOk = "250 " + new string('x', 65536 - 6) + "\r\n";
        SmtpRun run = await RunAsync(Greeting + EhloReply + longOk + Bye, Body("one\r\n"));

        Assert.AreEqual(CurlExitCode.TooLarge, run.Result.ExitCode);
        Assert.AreEqual("A value or data field grew larger than allowed", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailFromCarryingCrlf_SendsItRawAndReadsTheNextReplyForRcptAsCurlDoes()
    {
        // curl -K with mail-from = "a@b\r\nRSET": MAIL and the injected line go out as they
        // are, the injected line's 502 is read as RCPT's reply, and curl exits 55.
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + "502 Command not implemented\r\n" + Ok + Bye,
            Body("hi"),
            new MailRequestOptions { From = "a@b\r\nRSET", Recipients = ["c@d"] });

        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b\r\nRSET>\r\nRCPT TO:<c@d>\r\n" + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("RCPT failed: 502", run.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsync_MailRcptCarryingCrlf_SendsItRawAndReadsTheNextReplyForDataAsCurlDoes()
    {
        // curl -K with mail-rcpt = "c@d\r\nRSET": the injected line's 502 is read as DATA's
        // reply, and curl exits 55.
        SmtpRun run = await RunAsync(
            Greeting + EhloReply + Ok + Ok + "502 Command not implemented\r\n" + StartData + Bye,
            Body("hi"),
            new MailRequestOptions { From = "a@b", Recipients = ["c@d\r\nRSET"] });

        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\nRCPT TO:<c@d\r\nRSET>\r\nDATA\r\n" + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("DATA failed: 502", run.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("199", DisplayName = "199, below the 2xx range")]
    [DataRow("999", DisplayName = "999, the largest code")]
    [DataRow("600", DisplayName = "600, no reply class")]
    public async Task ExecuteAsync_MailAnsweredWithOutOfRangeCode_IsExit55AsCurlDoes(string code)
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + code + " odd\r\n" + Bye, Body("hi"));

        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\n" + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode);
        Assert.AreEqual("MAIL failed: " + code, run.Result.ErrorMessage);
        Assert.AreEqual(int.Parse(code, provider: null), run.Result.Report?.ResponseCode);
    }

    [TestMethod]
    [DataRow("garbage\r\n", DisplayName = "No code at all")]
    [DataRow("25\r\n", DisplayName = "Two digits")]
    [DataRow("2x0 no\r\n", DisplayName = "A letter inside the code")]
    [DataRow("\r\n", DisplayName = "An empty line")]
    [DataRow("2500 four digits\r\n", DisplayName = "A fourth digit")]
    public async Task ExecuteAsync_LineWithoutAReplyCodeBeforeTheReply_IsSkipped(string noise)
    {
        SmtpRun run = await RunAsync(Greeting + EhloReply + noise + Ok + Ok + StartData + Ok + Bye, Body("one\r\n"));

        Assert.AreEqual(Envelope + "one\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_EhloWithAThousandContinuationLines_IsReadAsOneReply()
    {
        string ehlo = string.Concat(Enumerable.Range(0, 1000).Select(index => $"250-EXT{index}\r\n")) + "250 OK\r\n";
        SmtpRun run = await RunAsync(Greeting + ehlo + Ok + Ok + StartData + Ok + Bye, Body("one\r\n"));

        Assert.AreEqual(Envelope + "one\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_TwoHundredRecipients_SendsOneRcptEachInOrder()
    {
        string[] recipients = [.. Enumerable.Range(0, 200).Select(index => $"r{index}@d")];
        string replies = Greeting + EhloReply + Ok + string.Concat(Enumerable.Repeat(Ok, 200)) + StartData + Ok + Bye;

        SmtpRun run = await RunAsync(replies, Body("one\r\n"), new MailRequestOptions { From = "a@b", Recipients = recipients });

        string rcpts = string.Concat(recipients.Select(recipient => $"RCPT TO:<{recipient}>\r\n"));
        Assert.AreEqual("EHLO dom\r\nMAIL FROM:<a@b>\r\n" + rcpts + "DATA\r\none\r\n.\r\n" + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerRunTwiceInParallel_KeepsEachSessionApart()
    {
        var first = new ScriptedConnection(Encoding.Latin1.GetBytes(Accepting));
        var second = new ScriptedConnection(Encoding.Latin1.GetBytes(Accepting));
        var handler = new SmtpProtocolHandler(
            new QueuedConnector(ConnectResult.Connected(first), ConnectResult.Connected(second)),
            new QueuedTlsProvider());

        TransferResult[] results = await Task.WhenAll(
            Task.Run(async () => await handler.ExecuteAsync(Context(Body("one\r\n")))),
            Task.Run(async () => await handler.ExecuteAsync(Context(Body("one\r\n")))));

        Assert.AreEqual(CurlExitCode.Ok, results[0].ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, results[1].ExitCode);
        Assert.AreEqual(Envelope + "one\r\n.\r\n" + Quit, Encoding.Latin1.GetString(first.Sent));
        Assert.AreEqual(Envelope + "one\r\n.\r\n" + Quit, Encoding.Latin1.GetString(second.Sent));
    }

    private static MemoryStream Body(string text) => new(Encoding.Latin1.GetBytes(text));

    private static TransferContext Context(Stream upload, MailRequestOptions? mail = null) => new()
    {
        Url = CurlUrl.Parse(Url),
        Output = Stream.Null,
        Upload = upload,
        Mail = mail ?? new MailRequestOptions { From = "a@b", Recipients = ["c@d"] },
        Progress = new RecordingProgress(),
    };

    private Task<SmtpRun> RunAsync(string replies, Stream upload, MailRequestOptions? mail = null) =>
        SmtpRun.ExecuteAsync(Diagnostics, Context(upload, mail), new ScriptedConnection(Encoding.Latin1.GetBytes(replies)));

    /// <summary>A readable, seekable stream that hands back at most one byte per read.</summary>
    private sealed class OneByteReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));

        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            base.ReadAsync(buffer, offset, Math.Min(count, 1), cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
    }
}
