using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Imap.Fakes;

namespace Curl.Protocol.Imap;

/// <summary>
/// Pins how an IMAP session reads its responses against curl 8.21.0: lines split across
/// reads, continuations no command asked for, NUL bytes, the 65535-byte line limit, literals
/// cut short and the server hanging up. The line limit, the continuation and the server
/// closing were measured with <c>Record-CurlExchange.ps1 -Imap</c> on 2026-09-28; the NUL
/// byte follows curl's <c>lib/pingpong.c</c> (BL-553 Notes).
/// </summary>
[TestClass]
public sealed class ImapProtocolHandlerResponseTests
{
    private const string Url = "imap://127.0.0.1:18143/";

    private const string Greeting = "* OK ready\r\n";

    private const string CapabilityReply = "* CAPABILITY IMAP4rev1\r\nA001 OK done\r\n";

    private const string LogoutReply = "* BYE Logging out\r\nA002 OK done\r\n";

    private const string Capability = "A001 CAPABILITY\r\n";

    private const string ResponseReadingFailed = "response reading failed (errno: 0)";

    [TestMethod]
    public async Task ExecuteAsync_ResponsesSplitAcrossReads_AreReadWhole()
    {
        byte[] replies = Latin1(Greeting + CapabilityReply + LogoutReply);

        ImapRun run = await ImapRun.ExecuteAsync(Url, new ScriptedConnection([.. replies.Chunk(3)]));

        Assert.AreEqual(Capability + "A002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LinesThatAreNeitherTaggedUntaggedNorContinuations_AreSkipped()
    {
        // An LF alone ends a line; "xy", "+abc" and "A0011 OK" are none of the three.
        ImapRun run = await RunAsync("xy\n* OK ready\n+abc\r\nA0011 OK\r\n" + CapabilityReply + LogoutReply);

        Assert.AreEqual(Capability + "A002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("+ go on\r\n", DisplayName = "CAPABILITY=+ go on (measured)")]
    [DataRow("+x\n", DisplayName = "a plus and one character before the LF")]
    [DataRow("+\r\n", DisplayName = "a bare plus")]
    public async Task ExecuteAsync_UnexpectedContinuation_FailsWithExit8(string continuation)
    {
        ImapRun run = await RunAsync(Greeting + continuation + CapabilityReply);

        Assert.AreEqual(Capability, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WeirdServerReply, "Unexpected continuation response"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContinuationInsteadOfAGreeting_FailsWithExit8()
    {
        ImapRun run = await RunAsync("+ hello\r\n");

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WeirdServerReply, "Unexpected continuation response"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_NulByteInALine_FailsWithExit8()
    {
        ImapRun run = await RunAsync(Greeting + "* CAPABILITY IMAP4rev1\0\r\nA001 OK done\r\n");

        Assert.AreEqual(Capability, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.WeirdServerReply, "Nul byte in server response line"),
            run.Result);
    }

    [TestMethod]
    [DataRow("", DisplayName = "before the greeting (GREETING=CLOSE)")]
    [DataRow(Greeting, DisplayName = "after CAPABILITY (CAPABILITY=CLOSE)")]
    [DataRow(Greeting + "* CAPABILITY IMAP4rev1\r\n", DisplayName = "mid-response")]
    [DataRow(Greeting + "* CAPABILITY {8}\r\nSTART", DisplayName = "mid-literal")]
    [DataRow(Greeting + "* CAPABILITY {8}\r\nSTARTTLS", DisplayName = "after a literal, before its line ends")]
    public async Task ExecuteAsync_ServerClosesBeforeAResponseIsComplete_FailsWithExit56(string replies)
    {
        ImapRun run = await RunAsync(replies);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, ResponseReadingFailed), run.Result);
        Assert.DoesNotContain("LOGOUT", run.Sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_ReadFails_FailsWithExit56()
    {
        var connection = new ScriptedConnection(Latin1(Greeting)) { FailReadsWhenExhausted = true };

        ImapRun run = await ImapRun.ExecuteAsync(Url, connection);

        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, ResponseReadingFailed), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_SendFails_FailsWithExit56WhenNoResponseFollows()
    {
        var connection = new ScriptedConnection(Latin1(Greeting)) { WritesBeforeFailure = 0 };

        ImapRun run = await ImapRun.ExecuteAsync(Url, connection);

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(TransferResult.Failure(CurlExitCode.RecvError, ResponseReadingFailed), run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseLineOf65536Bytes_FailsWithExit100()
    {
        // GREETING=* OK and 65529 x: 65534 characters and CRLF, exit 100, "A value or data
        // field grew larger than allowed".
        ImapRun run = await RunAsync("* OK " + new string('x', 65529) + "\r\n");

        Assert.AreEqual(string.Empty, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_ResponseLineOf65535Bytes_IsRead()
    {
        // GREETING=* OK and 65528 x: 65533 characters and CRLF, exit 0.
        ImapRun run = await RunAsync("* OK " + new string('x', 65528) + "\r\n" + CapabilityReply + LogoutReply);

        Assert.AreEqual(Capability + "A002 LOGOUT\r\n", run.Sent);
        Assert.AreEqual(TransferResult.Success(0), run.Result);
    }

    [TestMethod]
    [DataRow("* CAPABILITY {70000}\r\n", DisplayName = "a literal longer than a line")]
    [DataRow("* CAPABILITY {65530}\r\n", DisplayName = "a literal that overflows its line")]
    public async Task ExecuteAsync_LiteralBeyondTheLineLimit_FailsWithExit100(string untagged)
    {
        ImapRun run = await RunAsync(Greeting + untagged + new string('x', 70000) + "\r\nA001 OK done\r\n");

        Assert.AreEqual(Capability, run.Sent);
        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed"),
            run.Result);
    }

    [TestMethod]
    public async Task ExecuteAsync_LineAfterALiteralBeyondTheLineLimit_FailsWithExit100()
    {
        ImapRun run = await RunAsync(Greeting + "* CAPABILITY {65500}\r\n" + new string('x', 65500) + new string('y', 100) + "\r\nA001 OK done\r\n");

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.TooLarge, "A value or data field grew larger than allowed"),
            run.Result);
    }

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static Task<ImapRun> RunAsync(string replies) =>
        ImapRun.ExecuteAsync(Url, new ScriptedConnection(Latin1(replies)));
}
