using System.Collections.Concurrent;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Imap;

/// <summary>
/// Adversarial black-box tests (BL-1511, by <c>Documentation/Wiki/Adversarial-Testing.md</c>):
/// <see cref="ImapProtocolHandler" /> attacked through its public surface and injected fakes
/// with literal sizes, line lengths and UIDs at their limits, malformed and out-of-state
/// server replies, CRLF injected through the mailbox and <c>-X</c>, and replies split at every
/// byte, repeated and run concurrently. The oracle is the handler's documented contract,
/// which pins curl 8.21.0's measured behaviour.
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerAdversarialTests
{
    private const string Host = "imap://127.0.0.1:18155/";

    private const string Opening = "* OK ready\r\n* CAPABILITY IMAP4rev1\r\nA001 OK CAPABILITY completed\r\n";

    private const string SelectReply = "* 2 EXISTS\r\nA002 OK [READ-WRITE] SELECT completed\r\n";

    private const string Message = "Subject: x\r\n\r\nHello.\r\n";

    private const string FetchReply = "* 1 FETCH (UID 1 BODY[] {22}\r\n" + Message + ")\r\nA003 OK FETCH completed\r\n";

    private const string LogoutReply = "* BYE\r\nA004 OK LOGOUT completed\r\n";

    private const string SentThroughSelect = "A001 CAPABILITY\r\nA002 SELECT INBOX\r\n";

    /// <summary>The whole conversation of a fetch of <see cref="Message" /> that succeeds.</summary>
    private const string FetchConversation = Opening + SelectReply + FetchReply + LogoutReply;

    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // Boundaries

    [TestMethod]
    public async Task ExecuteAsync_FetchLiteralOfZeroBytes_SucceedsWithNothingWrittenAndLogsOut()
    {
        Run run = await RunAsync(
            Host + "INBOX;UID=1",
            Opening + SelectReply + "* 1 FETCH (BODY[] {0}\r\n)\r\nA003 OK done\r\n" + LogoutReply);

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("9223372036854775807", DisplayName = "long.MaxValue")]
    [DataRow("4294967296", DisplayName = "2^32")]
    public async Task ExecuteAsync_FetchLiteralHugeThenServerCloses_FailsWithExit18WithoutAllocatingIt(string size)
    {
        Run run = await RunAsync(
            Host + "INBOX;UID=1",
            Opening + SelectReply + "* 1 FETCH (BODY[] {" + size + "}\r\nabc");

        AssertResult(run, CurlExitCode.PartialFile);
        Assert.AreEqual(CurlExitCode.PartialFile, run.Result.ExitCode);
        Assert.AreEqual("abc", run.Output);
        Assert.AreEqual(3L, run.Result.BytesTransferred);
        Assert.IsFalse(run.Sent.Contains("LOGOUT", StringComparison.Ordinal), "No LOGOUT after a literal cut short.");
    }

    [TestMethod]
    [DataRow("{9223372036854775808}", DisplayName = "one past long.MaxValue")]
    [DataRow("{-1}", DisplayName = "negative")]
    [DataRow("{}", DisplayName = "no digits")]
    [DataRow("{12", DisplayName = "no closing brace")]
    [DataRow("{1 2}", DisplayName = "a space among the digits")]
    [DataRow("{+5}", DisplayName = "a sign")]
    [DataRow("{0x10}", DisplayName = "hexadecimal")]
    public async Task ExecuteAsync_FetchLiteralSizeNotDigitsAndBrace_FailsWithExit8AfterLogout(string literal)
    {
        Run run = await RunAsync(
            Host + "INBOX;UID=1",
            Opening + SelectReply + "* 1 FETCH (BODY[] " + literal + "\r\nA003 OK done\r\n" + LogoutReply);

        AssertResult(run, CurlExitCode.WeirdServerReply);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(string.Empty, run.Output);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    [DataRow("0", DisplayName = "UID 0")]
    [DataRow("4294967295", DisplayName = "UID 2^32-1")]
    [DataRow("4294967296", DisplayName = "UID 2^32, past 32 bits")]
    [DataRow("99999999999999999999999", DisplayName = "UID past long")]
    public async Task ExecuteAsync_UidAtAndPastItsLimits_IsSentAsTypedAndFetched(string uid)
    {
        Run run = await RunAsync(Host + "INBOX;UID=" + uid, FetchConversation);

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH " + uid + " BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(Message, run.Output);
        Assert.AreEqual(TransferResult.Success(22), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingLineOneByteUnderTheLimit_IsRead()
    {
        // 65534 bytes before the LF, the CR included: the longest line read.
        string greeting = "* OK " + new string('a', 65528) + "\r\n";

        Run run = await RunAsync(Host, greeting + "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n* LIST () \"/\" INBOX\r\nA002 OK done\r\n" + "* BYE\r\nA003 OK bye\r\n");

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual(CurlExitCode.Ok, run.Result.ExitCode);
        Assert.AreEqual("A001 CAPABILITY\r\nA002 LIST \"\" *\r\nA003 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_GreetingLineAtTheLimit_FailsWithExit100BeforeSendingAnything()
    {
        string greeting = "* OK " + new string('a', 65529) + "\r\n";

        Run run = await RunAsync(Host, greeting + "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n");

        AssertResult(run, CurlExitCode.TooLarge);
        Assert.AreEqual(CurlExitCode.TooLarge, run.Result.ExitCode);
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    [DataRow("{70000}", DisplayName = "a literal past the line limit")]
    [DataRow("{65535}", DisplayName = "a literal exactly the line limit")]
    public async Task ExecuteAsync_CapabilityLiteralPastTheLineLimit_FailsWithExit100WithoutReadingIt(string literal)
    {
        Run run = await RunAsync(Host, "* OK ready\r\n* CAPABILITY " + literal + "\r\nabc");

        AssertResult(run, CurlExitCode.TooLarge);
        Assert.AreEqual(CurlExitCode.TooLarge, run.Result.ExitCode);
        Assert.AreEqual("A001 CAPABILITY\r\n", run.Sent);
    }

    // Malformed input

    [TestMethod]
    [DataRow("%0D%0AA999%20DELETE%20INBOX", DisplayName = "percent-encoded CRLF")]
    [DataRow("%0AA999%20DELETE%20INBOX", DisplayName = "percent-encoded LF")]
    [DataRow("a%00b", DisplayName = "percent-encoded NUL")]
    public async Task ExecuteAsync_MailboxDecodingToAControlByte_FailsWithExit3AndInjectsNothing(string mailbox)
    {
        Run run = await RunAsync(Host + mailbox + ";UID=1", FetchConversation);

        AssertResult(run, CurlExitCode.UrlMalformat);
        Assert.AreEqual(CurlExitCode.UrlMalformat, run.Result.ExitCode);
        Assert.IsFalse(run.Sent.Contains("SELECT", StringComparison.Ordinal), "No SELECT for a malformed mailbox.");
        Assert.IsFalse(run.Sent.Contains("DELETE", StringComparison.Ordinal), "Nothing injected.");
    }

    [TestMethod]
    [DataRow("NOOP%0D%0AA999 DELETE INBOX", DisplayName = "percent-encoded CRLF")]
    [DataRow("NOOP\r\nA999 DELETE INBOX", DisplayName = "raw CRLF")]
    [DataRow("NOOP\nA999 DELETE INBOX", DisplayName = "raw LF")]
    [DataRow("NOOP%00", DisplayName = "percent-encoded NUL")]
    public async Task ExecuteAsync_CustomCommandHoldingALineBreak_FailsWithExit3AndInjectsNothing(string customCommand)
    {
        var context = Context(Host, new MailRequestOptions { CustomCommand = customCommand });

        Run run = await RunAsync(context, Opening + LogoutReply);

        AssertResult(run, CurlExitCode.UrlMalformat);
        Assert.AreEqual(CurlExitCode.UrlMalformat, run.Result.ExitCode);
        Assert.IsFalse(run.Sent.Contains("NOOP", StringComparison.Ordinal), "The command is not sent.");
        Assert.IsFalse(run.Sent.Contains("DELETE", StringComparison.Ordinal), "Nothing injected.");
    }

    [TestMethod]
    [DataRow("%22%22", "\"\\\"\\\"\"", DisplayName = "two quotes")]
    [DataRow("a%5C", "\"a\\\\\"", DisplayName = "a trailing backslash")]
    [DataRow("%5C%22", "\"\\\\\\\"\"", DisplayName = "a backslash then a quote")]
    public async Task ExecuteAsync_MailboxOfQuotesAndBackslashes_IsQuotedWithEachEscaped(string mailbox, string quoted)
    {
        Run run = await RunAsync(Host + mailbox + ";UID=1", FetchConversation);

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual("A001 CAPABILITY\r\nA002 SELECT " + quoted + "\r\nA003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    [DataRow("A0010 OK not ours\r\n", DisplayName = "a tag the real one prefixes")]
    [DataRow("A00 OK not ours\r\n", DisplayName = "a prefix of the real tag")]
    [DataRow("a001 OK not ours\r\n", DisplayName = "the real tag in lower case")]
    [DataRow("A002 OK a later tag\r\n", DisplayName = "the next command's tag")]
    [DataRow("A001OK no space\r\n", DisplayName = "the tag with no space after it")]
    public async Task ExecuteAsync_TaggedLineNotMatchingTheCommandsTag_IsIgnored(string foreign)
    {
        string replies = "* OK ready\r\n* CAPABILITY IMAP4rev1\r\n" + foreign + "A001 OK CAPABILITY completed\r\n"
            + SelectReply + FetchReply + LogoutReply;

        Run run = await RunAsync(Host + "INBOX;UID=1", replies);

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual(Message, run.Output);
        Assert.AreEqual(TransferResult.Success(22), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_OnlyForeignTagsThenServerCloses_FailsWithExit56()
    {
        Run run = await RunAsync(Host + "INBOX;UID=1", "* OK ready\r\nA002 OK\r\nA000 OK\r\nB001 OK\r\n");

        AssertResult(run, CurlExitCode.RecvError);
        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        Assert.AreEqual("A001 CAPABILITY\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_UntaggedLinesInterleavedBeforeTheFetch_AreSkipped()
    {
        string interleaved = "* 3 EXISTS\r\n* 1 RECENT\r\n* OK [ALERT] hello\r\n* 2 EXPUNGE\r\n";

        Run run = await RunAsync(
            Host + "INBOX;UID=1",
            Opening + interleaved + SelectReply + interleaved + FetchReply + LogoutReply);

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual(Message, run.Output);
        Assert.AreEqual(TransferResult.Success(22), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_FetchLineWithAQuotedBraceBeforeTheLiteral_ReadsTheQuotedBraceAsTheSize()
    {
        // curl's imap_state_fetch_resp takes the line's first '{', quoted or not: 5 bytes are
        // the literal, and the rest of the message is read as lines before the completion.
        Run run = await RunAsync(
            Host + "INBOX;UID=1",
            Opening + SelectReply + "* 1 FETCH (BODY[] \"{5}\" {22}\r\n" + Message + ")\r\nA003 OK done\r\n" + LogoutReply);

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual(Message[..5], run.Output);
        Assert.AreEqual(TransferResult.Success(5), run.Result);
    }

    // Invalid partitions

    [TestMethod]
    [DataRow("+ go on\r\n", DisplayName = "a continuation")]
    [DataRow("+\r\n", DisplayName = "a plus and exactly one character, the CR")]
    public async Task ExecuteAsync_ContinuationAnsweringCapability_FailsWithExit8(string continuation)
    {
        Run run = await RunAsync(Host + "INBOX;UID=1", "* OK ready\r\n" + continuation + "A001 OK done\r\n");

        AssertResult(run, CurlExitCode.WeirdServerReply);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual("A001 CAPABILITY\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinuationAnsweringFetch_FailsWithExit8()
    {
        Run run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + "+ send more\r\nA003 OK done\r\n" + LogoutReply);

        AssertResult(run, CurlExitCode.WeirdServerReply);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(string.Empty, run.Output);
    }

    [TestMethod]
    [DataRow("* BAD what\r\n", DisplayName = "an untagged BAD")]
    [DataRow("* NO go away\r\n", DisplayName = "an untagged NO")]
    [DataRow("* BYE leaving\r\n", DisplayName = "an untagged BYE")]
    public async Task ExecuteAsync_GreetingNotOkOrPreauth_FailsWithExit8BeforeSendingAnything(string greeting)
    {
        Run run = await RunAsync(Host + "INBOX;UID=1", greeting + "* OK ready\r\n" + "A001 OK done\r\n");

        AssertResult(run, CurlExitCode.WeirdServerReply);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, run.Result.ExitCode);
        Assert.AreEqual(string.Empty, run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_FetchCompletedWithNoUntaggedFetch_FailsWithExit78AfterLogout()
    {
        Run run = await RunAsync(Host + "INBOX;UID=1", Opening + SelectReply + "* 1 EXISTS\r\nA003 OK nothing\r\n" + LogoutReply);

        AssertResult(run, CurlExitCode.RemoteFileNotFound);
        Assert.AreEqual(CurlExitCode.RemoteFileNotFound, run.Result.ExitCode);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesRightAfterTheGreeting_FailsWithExit56()
    {
        Run run = await RunAsync(Host + "INBOX;UID=1", "* OK ready\r\n");

        AssertResult(run, CurlExitCode.RecvError);
        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerSendsNothing_FailsWithExit56WithoutSendingAnything()
    {
        Run run = await RunAsync(Host + "INBOX;UID=1", string.Empty);

        AssertResult(run, CurlExitCode.RecvError);
        Assert.AreEqual(CurlExitCode.RecvError, run.Result.ExitCode);
        Assert.AreEqual(string.Empty, run.Sent);
    }

    // State and concurrency

    [TestMethod]
    public async Task ExecuteAsync_EveryReplyByteInItsOwnRead_FetchesTheSameMessage()
    {
        byte[][] reads = [.. Latin1(FetchConversation).Select(static b => new[] { b })];

        Run run = await RunAsync(Context(Host + "INBOX;UID=1"), new ScriptedConnection(reads));

        AssertResult(run, CurlExitCode.Ok);
        Assert.AreEqual(Message, run.Output);
        Assert.AreEqual(TransferResult.Success(22), run.Result);
        Assert.AreEqual(SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ConversationSplitInTwoAtEveryOffset_FetchesTheSameMessage()
    {
        byte[] whole = Latin1(FetchConversation);
        for (int split = 1; split < whole.Length; split++)
        {
            var connection = new ScriptedConnection(whole[..split], whole[split..]);
            var context = Context(Host + "INBOX;UID=1");

            ImapRun run = await ImapRun.ExecuteAsync(context, connection);

            string output = Encoding.Latin1.GetString(((MemoryStream)context.Output).ToArray());
            Diagnostics.Assert($"result split at {split}", DiagnosticText.Result(TransferResult.Success(22)), DiagnosticText.Result(run.Result));
            Assert.AreEqual(TransferResult.Success(22), run.Result, $"split at {split}");
            Assert.AreEqual(Message, output, $"split at {split}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ServerClosesAtEveryOffsetInsideTheLiteral_FailsWithExit18AndWritesWhatArrived()
    {
        string head = Opening + SelectReply + "* 1 FETCH (UID 1 BODY[] {22}\r\n";
        for (int cut = 0; cut < Message.Length; cut++)
        {
            Run run = await RunAsync(Host + "INBOX;UID=1", head + Message[..cut]);

            Assert.AreEqual(CurlExitCode.PartialFile, run.Result.ExitCode, $"cut at {cut}");
            Assert.AreEqual(Message[..cut], run.Output, $"cut at {cut}");
            Assert.AreEqual((long)cut, run.Result.BytesTransferred, $"cut at {cut}");
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerTwiceInARow_FetchesBothWithTagsStartingAgain()
    {
        var connections = new ConcurrentQueue<ScriptedConnection>(
            [new ScriptedConnection(Latin1(FetchConversation)), new ScriptedConnection(Latin1(FetchConversation))]);
        var handler = new ImapProtocolHandler(new EachCallConnector(connections), new QueuedTlsProvider());
        ScriptedConnection[] used = [.. connections];

        TransferResult first = await handler.ExecuteAsync(Context(Host + "INBOX;UID=1"));
        TransferResult second = await handler.ExecuteAsync(Context(Host + "INBOX;UID=1"));

        Assert.AreEqual(TransferResult.Success(22), first);
        Assert.AreEqual(TransferResult.Success(22), second);
        Assert.AreEqual(Encoding.Latin1.GetString(used[0].Sent), Encoding.Latin1.GetString(used[1].Sent));
    }

    [TestMethod]
    public async Task ExecuteAsync_SameHandlerOnSixteenTransfersAtOnce_EachFetchesItsOwnMessage()
    {
        const int Transfers = 16;
        var connections = new ConcurrentQueue<ScriptedConnection>(
            Enumerable.Range(0, Transfers).Select(static _ => new ScriptedConnection([.. Latin1(FetchConversation).Chunk(7)])));
        ScriptedConnection[] used = [.. connections];
        var handler = new ImapProtocolHandler(new EachCallConnector(connections), new QueuedTlsProvider());
        TransferContext[] contexts = [.. Enumerable.Range(0, Transfers).Select(_ => Context(Host + "INBOX;UID=1"))];

        TransferResult[] results = await Task.WhenAll(contexts.Select(context => Task.Run(async () => await handler.ExecuteAsync(context))));

        for (int index = 0; index < Transfers; index++)
        {
            Assert.AreEqual(TransferResult.Success(22), results[index], $"transfer {index}");
            Assert.AreEqual(Message, Encoding.Latin1.GetString(((MemoryStream)contexts[index].Output).ToArray()), $"transfer {index}");
            Assert.AreEqual(
                SentThroughSelect + "A003 UID FETCH 1 BODY[]\r\nA004 LOGOUT\r\n",
                Encoding.Latin1.GetString(used[index].Sent),
                $"connection {index}");
        }
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static TransferContext Context(string url, MailRequestOptions? mail = null) =>
        new()
        {
            Url = CurlUrl.Parse(url),
            Output = new MemoryStream(),
            Mail = mail,
        };

    private Task<Run> RunAsync(string url, string replies) =>
        RunAsync(Context(url), replies);

    private Task<Run> RunAsync(TransferContext context, string replies)
    {
        Diagnostics.Arrange("server", DiagnosticText.Escape(replies.Length > 400 ? replies[..400] + "..." : replies));
        return RunAsync(context, new ScriptedConnection(Latin1(replies)));
    }

    private async Task<Run> RunAsync(TransferContext context, ScriptedConnection connection)
    {
        Diagnostics.Arrange("url", context.Url.ToString());
        ImapRun run = await ImapRun.ExecuteAsync(context, connection);
        string output = Encoding.Latin1.GetString(((MemoryStream)context.Output).ToArray());
        Diagnostics.Act("result", DiagnosticText.Result(run.Result));
        Diagnostics.Act("sent", DiagnosticText.Escape(run.Sent));
        Diagnostics.Act("output", DiagnosticText.Escape(output.Length > 200 ? output[..200] + "..." : output));
        return new Run(run.Result, run.Sent, output);
    }

    private void AssertResult(Run run, CurlExitCode expected) =>
        Diagnostics.Assert("exit code", expected, run.Result.ExitCode);

    /// <summary>What a transfer did: its result, the bytes sent and the output written.</summary>
    private sealed record Run(TransferResult Result, string Sent, string Output);

    /// <summary>
    /// An <see cref="IConnector" /> that answers each connect, from any thread, with the next
    /// connection of <paramref name="connections" />.
    /// </summary>
    private sealed class EachCallConnector(ConcurrentQueue<ScriptedConnection> connections) : IConnector
    {
        public ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken) =>
            ValueTask.FromResult(connections.TryDequeue(out ScriptedConnection? next)
                ? ConnectResult.Connected(next)
                : throw new InvalidOperationException("No connection left."));
    }
}
