using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Nats;

/// <summary>
/// Checks the wire format against the NATS client protocol's named sections ("Protocol conventions",
/// "INFO", "CONNECT", "PUB", "MSG"): the lines the client writes and the ones it reads.
/// </summary>
[TestClass]
public sealed class NatsWireTests
{
    [TestMethod]
    public void ConnectLineFor_NoCredentials_GivesTheClientFieldsOnly() =>
        Assert.AreEqual(
            "CONNECT {\"verbose\":false,\"pedantic\":false,\"tls_required\":true,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1}\r\n",
            Encoding.UTF8.GetString(NatsConnect.LineFor(true, null)));

    [TestMethod]
    [DataRow("", "", "")]
    [DataRow("tok", "", ",\"auth_token\":\"tok\"")]
    [DataRow("al", "pw", ",\"user\":\"al\",\"pass\":\"pw\"")]
    public void ConnectLineFor_Credentials_GivesUserAndPassOrAToken(string user, string password, string fields) =>
        Assert.AreEqual(
            $"CONNECT {{\"verbose\":false,\"pedantic\":false,\"tls_required\":false,\"name\":\"curl\",\"lang\":\"curl\",\"version\":\"1.0.0\",\"protocol\":1{fields}}}\r\n",
            Encoding.UTF8.GetString(NatsConnect.LineFor(false, new NetworkCredential(user, password))));

    [TestMethod]
    [DataRow(null, "PUB a.b 2\r\nhi\r\n")]
    [DataRow("_INBOX.x", "PUB a.b _INBOX.x 2\r\nhi\r\n")]
    public void Publish_GivesTheLengthThenThePayload(string? replyTo, string expected) =>
        Assert.AreEqual(expected, Encoding.UTF8.GetString(NatsConnect.Publish("a.b", replyTo, "hi"u8)));

    [TestMethod]
    [DataRow("MSG a.b 1 5", "a.b", "1", null, 5)]
    [DataRow("msg\ta.b  9 _INBOX.r 0", "a.b", "9", "_INBOX.r", 0)]
    public void MessageLineParse_FourOrFiveFields_ReadsThem(string line, string subject, string sid, string? replyTo, int length) =>
        Assert.AreEqual(new NatsMessageLine(subject, sid, replyTo, length), NatsMessageLine.Parse(line, 10));

    [TestMethod]
    [DataRow("MSG a.b 5")]
    [DataRow("MSG a b c d e")]
    [DataRow("PUB a.b 1 5")]
    [DataRow("MSG a.b 1 -5")]
    [DataRow("MSG a.b 1 11")]
    public void MessageLineParse_NotAMessageWithinTheLimit_FailsWithExit8(string line)
    {
        NatsException failure = Assert.ThrowsExactly<NatsException>(() => NatsMessageLine.Parse(line, 10));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual($"NATS server sent an unexpected line: {line}", failure.Message);
    }

    [TestMethod]
    [DataRow("INFO {}", false, NatsServerInfo.DefaultMaxPayload)]
    [DataRow("info {\"tls_required\":true,\"max_payload\":64}", true, 64L)]
    [DataRow("INFO {\"tls_required\":\"yes\",\"max_payload\":\"64\"}", false, NatsServerInfo.DefaultMaxPayload)]
    [DataRow("INFO {\"max_payload\":1.5}", false, NatsServerInfo.DefaultMaxPayload)]
    [DataRow("INFO {\"max_payload\":0}", false, NatsServerInfo.DefaultMaxPayload)]
    public void ServerInfoParse_ReadsTlsRequiredAndMaxPayload(string line, bool tlsRequired, long maxPayload) =>
        Assert.AreEqual(new NatsServerInfo(tlsRequired, maxPayload), NatsServerInfo.Parse(line));

    [TestMethod]
    [DataRow("", NatsMessages.NoInfo)]
    [DataRow("INFO", NatsMessages.NoInfo)]
    [DataRow("PING {}", NatsMessages.NoInfo)]
    [DataRow("INFO [1]", NatsMessages.BadInfo)]
    [DataRow("INFO {", NatsMessages.BadInfo)]
    public void ServerInfoParse_NotInfoWithAnObject_FailsWithExit8(string line, string message)
    {
        NatsException failure = Assert.ThrowsExactly<NatsException>(() => NatsServerInfo.Parse(line));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    [DataRow("nats://h/a.b", "a.b")]
    [DataRow("nats://h/a.%3E", "a.>")]
    public void SubjectOf_ThePath_IsTheDecodedSubject(string url, string subject) =>
        Assert.AreEqual(subject, NatsSubject.Of(CurlUrl.Parse(url)));

    [TestMethod]
    [DataRow("nats://h/", NatsMessages.NoSubject)]
    [DataRow("nats://h/a%20b", "NATS subject \"a b\" holds whitespace or an empty token")]
    [DataRow("nats://h/a..b", "NATS subject \"a..b\" holds whitespace or an empty token")]
    public void SubjectOf_NoSubjectOrABadOne_FailsWithExit3(string url, string message)
    {
        NatsException failure = Assert.ThrowsExactly<NatsException>(() => NatsSubject.Of(CurlUrl.Parse(url)));

        Assert.AreEqual(CurlExitCode.UrlMalformat, failure.ExitCode);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public void OperationOf_AnEmptyLine_IsEmpty() =>
        Assert.AreEqual(string.Empty, NatsOperations.OperationOf(" "));

    [TestMethod]
    public async Task ReadLineAsync_LinesSplitAcrossReads_GivesEachWithoutItsLineEnd()
    {
        NatsWireReader reader = new(ScriptedConnection.Of("PI", "NG\r\nPO", "NG\nMSG a 1 3\r\nabc\r\n"));

        Assert.AreEqual("PING", await reader.ReadLineAsync(CancellationToken.None));
        Assert.AreEqual("PONG", await reader.ReadLineAsync(CancellationToken.None));
        Assert.AreEqual("MSG a 1 3", await reader.ReadLineAsync(CancellationToken.None));
        Assert.AreEqual("abc", Encoding.UTF8.GetString(await reader.ReadPayloadAsync(3, CancellationToken.None)));
        Assert.IsNull(await reader.ReadLineAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadLineAsync_ClosedInsideALine_FailsWithExit56()
    {
        NatsException failure = await Assert.ThrowsExactlyAsync<NatsException>(async () => await new NatsWireReader(ScriptedConnection.Of("PIN")).ReadLineAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(NatsMessages.ClosedMidMessage, failure.Message);
    }

    [TestMethod]
    public async Task ReadLineAsync_ALineOverTheLimitWithoutItsEnd_FailsWithExit8() =>
        await AssertLineTooLongAsync(new string('x', 16384), new string('x', 16384), new string('x', 16384), new string('x', 16384), "x");

    [TestMethod]
    public async Task ReadLineAsync_ALineOverTheLimitEndedInTheLastRead_FailsWithExit8() =>
        await AssertLineTooLongAsync(new string('x', 16384), new string('x', 16384), new string('x', 16384), new string('x', 16384), "x\r\n");

    [TestMethod]
    public async Task ReadPayloadAsync_SplitAcrossReads_GivesThePayload()
    {
        NatsWireReader reader = new(ScriptedConnection.Of("ab", "cd\r", "\n"));

        Assert.AreEqual("abcd", Encoding.UTF8.GetString(await reader.ReadPayloadAsync(4, CancellationToken.None)));
    }

    [TestMethod]
    public async Task ReadPayloadAsync_ClosedInsideThePayload_FailsWithExit56()
    {
        NatsException failure = await Assert.ThrowsExactlyAsync<NatsException>(async () => await new NatsWireReader(ScriptedConnection.Of("ab")).ReadPayloadAsync(4, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
    }

    [TestMethod]
    [DataRow("abcdXY")]
    [DataRow("abcd\rY")]
    public async Task ReadPayloadAsync_NotEndedByCrlf_FailsWithExit8(string bytes)
    {
        NatsException failure = await Assert.ThrowsExactlyAsync<NatsException>(async () => await new NatsWireReader(ScriptedConnection.Of(bytes)).ReadPayloadAsync(4, CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual(NatsMessages.PayloadNotEndedByCrlf, failure.Message);
    }

    private static async Task AssertLineTooLongAsync(params string[] chunks)
    {
        NatsException failure = await Assert.ThrowsExactlyAsync<NatsException>(async () => await new NatsWireReader(ScriptedConnection.Of(chunks)).ReadLineAsync(CancellationToken.None));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual(NatsMessages.LineTooLong, failure.Message);
    }
}
