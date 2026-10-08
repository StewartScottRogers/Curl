using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Attacks <see cref="Pop3ProtocolHandler" /> as a black box, by the method in
/// <c>Documentation/Wiki/Adversarial-Testing.md</c> (BL-1514): message numbers at and past
/// <see cref="int.MaxValue" />, status lines that are nearly <c>+OK</c> or <c>-ERR</c>, a
/// dot-stuffed body cut at every byte, malformed APOP timestamps, CRLF smuggled through the
/// URL path and <c>-X</c>, and one handler reused and run on many tasks at once. Every
/// expected answer that curl shows on the wire was recorded from curl 8.21.0 (the Schannel
/// build) on 2026-10-07 with <c>Record-CurlExchange.ps1 -Pop3</c> (BL-1514 Notes).
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerAdversarialTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Greeting = "+OK POP3 ready <1896.697170952@localhost>\r\n";

    private const string UserOnlyCapaReply = "+OK\r\nUSER\r\n.\r\n";

    private const string Message = "Subject: a\r\n\r\nline one\r\n..two dots\r\n.one dot\r\nlast\r\n";

    private const string RetrReply =
        "+OK 52 octets\r\nSubject: a\r\n\r\nline one\r\n...two dots\r\n..one dot\r\nlast\r\n.\r\n";

    private const string EmptyListing = "+OK 0 messages\r\n.\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Capa = "CAPA\r\n";

    private const string Retr1 = "RETR 1\r\n";

    private const string List = "LIST\r\n";

    private const string Quit = "QUIT\r\n";

    private const string UserAndPass = "USER u\r\nPASS p\r\n";

    private const string Url = "pop3://127.0.0.1:18110/";

    private static readonly NetworkCredential UserP = new("u", "p");

    private static readonly TransferResult UrlMalformat =
        TransferResult.Failure(CurlExitCode.UrlMalformat, "URL using bad/illegal format or missing URL");

    private static readonly TransferResult WeirdServerReply =
        TransferResult.Failure(CurlExitCode.WeirdServerReply, "Weird server reply");

    [TestMethod]
    [DataRow("0", DisplayName = "message 0")]
    [DataRow("-1", DisplayName = "message -1")]
    [DataRow("2147483647", DisplayName = "message int.MaxValue")]
    [DataRow("2147483648", DisplayName = "message int.MaxValue + 1")]
    [DataRow("99999999999999999999", DisplayName = "message past long.MaxValue")]
    public async Task ExecuteAsync_MessageNumberAtOrPastAnIntegerBoundary_IsSentVerbatimAndItsErrIsExit8(string number)
    {
        // Measured for 0, -1, 2147483648 and 99999999999999999999: RETR <number> verbatim, -ERR, QUIT, exit 8.
        Pop3Run run = await RunAsync(Url + number, null, null, Greeting, UserOnlyCapaReply, "-ERR no such message\r\n", Bye);

        Diagnostics.Diff("sent", Capa + "RETR " + number + "\r\n" + Quit, run.Sent);
        Assert.AreEqual(Capa + "RETR " + number + "\r\n" + Quit, run.Sent);
        Diagnostics.AssertResult(WeirdServerReply, run.Result);
        Assert.AreEqual(WeirdServerReply, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_DotStuffedBodyCutAtEveryByte_WritesTheSameUnstuffedMessage()
    {
        var mismatches = new List<string>();
        for (int cut = 1; cut < RetrReply.Length; cut++)
        {
            Pop3Run run = await RunAsync(Url + "1", null, null, Greeting, UserOnlyCapaReply, RetrReply[..cut], RetrReply[cut..], Bye);
            string written = Encoding.Latin1.GetString(run.Output);
            if (written != Message || run.Result != TransferResult.Success(Message.Length) || run.Sent != Capa + Retr1 + Quit)
            {
                mismatches.Add($"cut {cut}: {run.Result}, wrote {written.Length} bytes");
            }
        }

        Diagnostics.AssertValues("cuts that changed the answer", 0, mismatches.Count);
        Assert.IsEmpty(mismatches, string.Join("; ", mismatches));
    }

    [TestMethod]
    [DataRow("+OK\r\nabc\r\n.", DisplayName = "a dot with no CRLF")]
    [DataRow("+OK\r\nabc\r\n.\r", DisplayName = "a dot and a CR")]
    [DataRow("+OK\r\nabc\r", DisplayName = "a lone CR")]
    [DataRow("+OK\r\n..", DisplayName = "a stuffed dot with no CRLF")]
    public async Task ExecuteAsync_ServerHangsUpInsideTheTerminator_SucceedsWritingAPrefixOfTheBodyWithoutQuit(string reply)
    {
        // The recorder always ends a reply in CRLF, so these cannot be measured; the oracle is that
        // the run ends, sends no QUIT on a dead connection, and writes no byte the server did not send.
        Pop3Run run = await RunAsync(Url + "1", null, null, Greeting, UserOnlyCapaReply, reply);
        string written = Encoding.Latin1.GetString(run.Output);
        string body = reply[(reply.IndexOf('\n', StringComparison.Ordinal) + 1)..];

        Diagnostics.Diff("sent", Capa + Retr1, run.Sent);
        Assert.AreEqual(Capa + Retr1, run.Sent);
        Diagnostics.AssertValues("written is a prefix of the body", true, body.StartsWith(written, StringComparison.Ordinal));
        Assert.StartsWith(written, body);
        Assert.AreEqual(TransferResult.Success(written.Length), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_StatusLineOkFollowedByLetters_CountsAsOkAndWritesTheBody()
    {
        // Measured: RETR=+OKAY, then body, then QUIT, exit 0, "body\r\n" written.
        Pop3Run run = await RunAsync(Url + "1", null, null, Greeting, UserOnlyCapaReply, "+OKAY\r\nbody\r\n.\r\n", Bye);

        Diagnostics.Diff("sent", Capa + Retr1 + Quit, run.Sent);
        Assert.AreEqual(Capa + Retr1 + Quit, run.Sent);
        Diagnostics.Diff("output", "body\r\n", Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual("body\r\n", Encoding.Latin1.GetString(run.Output));
        Assert.AreEqual(TransferResult.Success(6), run.Result);
    }

    [TestMethod]
    [DataRow("+ok\r\nbody\r\n.\r\n", DisplayName = "RETR=+ok lower case")]
    [DataRow("-ERRx\r\n", DisplayName = "RETR=-ERRx")]
    public async Task ExecuteAsync_StatusLineNearlyOkOrErr_FailsWithExit8AndStillQuits(string reply)
    {
        // Measured: exit 8 "Weird server reply", nothing written, QUIT sent.
        Pop3Run run = await RunAsync(Url + "1", null, null, Greeting, UserOnlyCapaReply, reply, Bye);

        Diagnostics.Diff("sent", Capa + Retr1 + Quit, run.Sent);
        Assert.AreEqual(Capa + Retr1 + Quit, run.Sent);
        Assert.IsEmpty(run.Output);
        Diagnostics.AssertResult(WeirdServerReply, run.Result);
        Assert.AreEqual(WeirdServerReply, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LowerCaseErrLine_IsNotAStatusLineSoTheHangUpIsExit56WithoutQuit()
    {
        // Measured: RETR=-err no, then the recorder's hang-up: exit 56, no QUIT.
        Pop3Run run = await RunAsync(Url + "1", null, null, Greeting, UserOnlyCapaReply, "-err no\r\n");

        Diagnostics.Diff("sent", Capa + Retr1, run.Sent);
        Assert.AreEqual(Capa + Retr1, run.Sent);
        Assert.IsEmpty(run.Output);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, "response reading failed (errno: 0)"), run.Result);
    }

    [TestMethod]
    [DataRow("+OK <@>", "APOP u 356b00ac78088684492d155c031a0452\r\n", DisplayName = "GREETING=+OK <@>: the shortest timestamp")]
    [DataRow("+OK<a@b>", "APOP u e448b4da727938a4e29f25d416b01191\r\n", DisplayName = "GREETING=+OK<a@b>: no space after +OK")]
    [DataRow("+OK <nohost>", UserAndPass, DisplayName = "GREETING=+OK <nohost>: no @")]
    [DataRow("+OK <a@b", UserAndPass, DisplayName = "GREETING=+OK <a@b: never closed")]
    [DataRow("+OK a@b>", UserAndPass, DisplayName = "GREETING=+OK a@b>: never opened")]
    public async Task ExecuteAsync_MalformedApopTimestamp_LogsInAsCurlDoes(string greeting, string login)
    {
        // Measured with CAPA=+OK USER and -u u:p; each digest is the one curl sent.
        string loggedIn = login == UserAndPass ? "+OK\r\n+OK\r\n" : "+OK\r\n";
        Pop3Run run = await RunAsync(Url, UserP, null, greeting + "\r\n", UserOnlyCapaReply, loggedIn, EmptyListing, Bye);

        Diagnostics.Diff("sent", Capa + login + List + Quit, run.Sent);
        Assert.AreEqual(Capa + login + List + Quit, run.Sent);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
    }

    [TestMethod]
    [DataRow("%0d%0aDELE%201", DisplayName = "URL path /%0d%0aDELE%201")]
    [DataRow("1%0d%0aDELE%201", DisplayName = "URL path /1%0d%0aDELE%201")]
    [DataRow("1%0aDELE%201", DisplayName = "URL path /1%0aDELE%201, LF alone")]
    public async Task ExecuteAsync_CrlfSmuggledThroughTheUrlPath_SendsNoSecondCommandAndFailsWithExit3(string path)
    {
        Pop3Run run = await RunAsync(Url + path, null, null, Greeting, UserOnlyCapaReply, Bye);

        Diagnostics.Diff("sent", Capa + Quit, run.Sent);
        Assert.AreEqual(Capa + Quit, run.Sent);
        Diagnostics.AssertResult(UrlMalformat, run.Result);
        Assert.AreEqual(UrlMalformat, run.Result);
    }

    [TestMethod]
    [DataRow("NOOP\r\nDELE 1", DisplayName = "-X with a raw CRLF")]
    [DataRow("NOOP\nDELE 1", DisplayName = "-X with a raw LF")]
    [DataRow("NOOP%0d%0aDELE 1", DisplayName = "-X with an escaped CRLF")]
    [DataRow("NOOP%0D%0ADELE 1", DisplayName = "-X with an upper-case escaped CRLF")]
    public async Task ExecuteAsync_CrlfSmuggledThroughTheCustomCommand_SendsNoSecondCommandAndFailsWithExit3(string custom)
    {
        Pop3Run run = await RunAsync(Url, null, new MailRequestOptions { CustomCommand = custom }, Greeting, UserOnlyCapaReply, Bye);

        Diagnostics.Diff("sent", Capa + Quit, run.Sent);
        Assert.AreEqual(Capa + Quit, run.Sent);
        Diagnostics.AssertResult(UrlMalformat, run.Result);
        Assert.AreEqual(UrlMalformat, run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OneHandlerRunTwiceInARow_GivesTheSecondRunTheSameAnswerAsTheFirst()
    {
        var connections = new[]
        {
            Script(Greeting, UserOnlyCapaReply, RetrReply[..40], RetrReply[40..], Bye),
            Script(Greeting, UserOnlyCapaReply, RetrReply, Bye),
        };
        var handler = new Pop3ProtocolHandler(new HandingOutConnector(connections), new QueuedTlsProvider());

        for (int run = 0; run < connections.Length; run++)
        {
            using var output = new MemoryStream();
            TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url + "1"), Output = output });

            Diagnostics.Diff($"run {run} output", Message, Encoding.Latin1.GetString(output.ToArray()));
            Assert.AreEqual(Message, Encoding.Latin1.GetString(output.ToArray()));
            Assert.AreEqual(TransferResult.Success(Message.Length), result);
            Assert.AreEqual(Capa + Retr1 + Quit, Encoding.Latin1.GetString(connections[run].Sent));
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_OneHandlerOnSixteenTasksAtOnce_GivesEveryTransferItsOwnMessage()
    {
        const int Transfers = 16;
        ScriptedConnection[] connections = [.. Enumerable.Range(0, Transfers).Select(
            index => Script(Greeting, UserOnlyCapaReply, $"+OK\r\nmessage {index}\r\n.\r\n", Bye))];
        var handler = new Pop3ProtocolHandler(new HandingOutConnector(connections), new QueuedTlsProvider());

        (TransferResult Result, string Written)[] runs = await Task.WhenAll(Enumerable.Range(0, Transfers).Select(async _ =>
        {
            using var output = new MemoryStream();
            TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse(Url + "1"), Output = output });
            return (result, Encoding.Latin1.GetString(output.ToArray()));
        }));

        string[] written = [.. runs.Select(run => run.Written).Order(StringComparer.Ordinal)];
        string[] expected = [.. Enumerable.Range(0, Transfers).Select(index => $"message {index}\r\n").Order(StringComparer.Ordinal)];
        Diagnostics.AssertValues("transfers that failed", 0, runs.Count(run => run.Result.ExitCode != CurlExitCode.Ok));
        Assert.IsTrue(runs.All(run => run.Result.ExitCode == CurlExitCode.Ok));
        CollectionAssert.AreEqual(expected, written);
    }

    private static ScriptedConnection Script(params string[] reads) =>
        new([.. reads.Select(Encoding.Latin1.GetBytes)]);

    private async Task<Pop3Run> RunAsync(string url, NetworkCredential? credential, MailRequestOptions? mail, params string[] reads)
    {
        ScriptedConnection connection = Script(reads);
        using var output = new MemoryStream();
        var progress = new RecordingProgress();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse(url),
            Output = output,
            Progress = progress,
            Credentials = credential,
            Mail = mail,
        };
        var connector = new QueuedConnector(ConnectResult.Connected(connection));
        var tls = new QueuedTlsProvider();

        Diagnostics.ArrangeRun(url, connection.Script);
        TransferResult result = await new Pop3ProtocolHandler(connector, tls).ExecuteAsync(context);

        var run = new Pop3Run(result, connection, connector, tls, output.ToArray(), progress);
        Diagnostics.ActRun(run);
        return run;
    }

    /// <summary>Hands each connect the next of its connections, safely from many tasks at once.</summary>
    private sealed class HandingOutConnector(IEnumerable<ScriptedConnection> connections) : IConnector
    {
        private readonly ConcurrentQueue<ScriptedConnection> waiting = new(connections);

        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(waiting.TryDequeue(out ScriptedConnection? connection)
                ? ConnectResult.Connected(connection)
                : throw new InvalidOperationException("No connection left to hand out."));
    }
}
