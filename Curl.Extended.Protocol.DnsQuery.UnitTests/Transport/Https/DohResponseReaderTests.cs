using System.Text;
using Curl.Extended.Protocol.DnsQuery.Client;
using Curl.Extended.Protocol.DnsQuery.Transport.Https;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Transport.Https;

/// <summary>
/// Checks reading a DoH server's HTTP/1.1 response over a fake connection (RFC 8484 section 4.2): the body framed by
/// <c>Content-Length</c>, by chunks or by the connection's end, and every way the response can fail.
/// </summary>
[TestClass]
public sealed class DohResponseReaderTests
{
    private const string Head200 = "HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\n";
    private static readonly byte[] Answer = [0x12, 0x34, 0x81, 0x80, 0x00, 0x00, 0x00, 0x01];

    [TestMethod]
    [DataRow(1)]
    [DataRow(int.MaxValue)]
    public async Task ReadAnswerAsync_Rfc8484Section4_2_ContentLength_ReadsThatManyOctets(int readSize)
    {
        byte[] answer = await ReadAsync(Bytes($"{Head200}Content-Length: 8\r\n\r\n", Answer, new byte[] { 0xEE }), readSize);

        CollectionAssert.AreEqual(Answer, answer);
    }

    [TestMethod]
    public async Task ReadAnswerAsync_Rfc8484Section4_2_Chunked_JoinsTheChunksAndSkipsTheTrailer()
    {
        byte[] response = Bytes($"{Head200}Transfer-Encoding: chunked\r\n\r\n3;ext=1\r\n", Answer[..3], "\r\n5\r\n", Answer[3..], "\r\n0\r\nX-Trailer: t\r\n\r\n");

        byte[] answer = await ReadAsync(response, 2);

        CollectionAssert.AreEqual(Answer, answer);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(int.MaxValue)]
    public async Task ReadAnswerAsync_NoLength_ReadsToTheConnectionsEnd(int readSize)
    {
        byte[] answer = await ReadAsync(Bytes($"{Head200}\r\n", Answer), readSize);

        CollectionAssert.AreEqual(Answer, answer);
    }

    [TestMethod]
    public async Task ReadAnswerAsync_InterimResponse_SkipsItAndReadsTheFinalOne()
    {
        byte[] answer = await ReadAsync(Bytes($"HTTP/1.1 103 Early Hints\r\nLink: x\r\n\r\n{Head200}Content-Length: 8\r\n\r\n", Answer));

        CollectionAssert.AreEqual(Answer, answer);
    }

    [TestMethod]
    public async Task ReadAnswerAsync_Rfc8484Section4_2_1_NotFound_FailsWithExit22()
    {
        DnsQueryException failure = await FailureOfAsync(Bytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\n\r\n"));

        Assert.AreEqual(CurlExitCode.HttpReturnedError, failure.ExitCode);
    }

    [TestMethod]
    [DataRow("Content-Length: 9\r\n\r\n")]
    [DataRow("Transfer-Encoding: chunked\r\n\r\n9\r\n")]
    [DataRow("Transfer-Encoding: chunked\r\n\r\n")]
    [DataRow("Content-Le")]
    public async Task ReadAnswerAsync_ConnectionClosesEarly_FailsWithExit56(string rest)
    {
        DnsQueryException failure = await FailureOfAsync(Bytes(Head200 + rest, Answer));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual("The DoH server closed the connection before its whole answer arrived", failure.Message);
    }

    [TestMethod]
    [DataRow("zz\r\n")]
    [DataRow("FFFFFFFF\r\n")]
    [DataRow("10000\r\n")]
    [DataRow("8\r\n12345678X\r\n0\r\n\r\n")]
    [DataRow("FFF0\r\n")]
    public async Task ReadAnswerAsync_MalformedChunk_FailsWithExit8(string chunks)
    {
        byte[] body = chunks == "FFF0\r\n" ? Bytes(chunks, new byte[0xFFF0], "\r\n20\r\n") : Bytes(chunks);

        DnsQueryException failure = await FailureOfAsync(Bytes($"{Head200}Transfer-Encoding: chunked\r\n\r\n", body));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReadAnswerAsync_BodyLongerThanADnsMessage_FailsWithExit8()
    {
        DnsQueryException failure = await FailureOfAsync(Bytes($"{Head200}\r\n", new byte[DohResponseHead.MaxMessageLength + 1]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    [TestMethod]
    public async Task ReadAnswerAsync_LineLongerThanTheBuffer_FailsWithExit8()
    {
        DnsQueryException failure = await FailureOfAsync(Bytes($"{Head200}X: {new string('a', DohResponseReader.MaxLineLength)}\r\n\r\n"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
        Assert.AreEqual("The DoH server sent a line longer than 8192 octets", failure.Message);
    }

    [TestMethod]
    public async Task ReadAnswerAsync_TooManyHeaderLines_FailsWithExit8()
    {
        string headers = string.Concat(Enumerable.Repeat("X: y\r\n", DohResponseReader.MaxHeaderLines + 1));

        DnsQueryException failure = await FailureOfAsync(Bytes($"{Head200}{headers}\r\n"));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, failure.ExitCode);
    }

    private static byte[] Bytes(params object[] parts) =>
        [.. parts.SelectMany(part => part as byte[] ?? Encoding.ASCII.GetBytes((string)part))];

    private static async Task<byte[]> ReadAsync(byte[] response, int readSize = int.MaxValue)
    {
        var server = new ScriptedDnsServer((_, _) => [], response) { TcpReadSize = readSize };
        ConnectResult connect = await server.ConnectAsync(new ConnectTarget("h", 443, UseTls: false), CancellationToken.None);
        return await DohResponseReader.ReadAnswerAsync(connect.Connection!, CancellationToken.None);
    }

    private static Task<DnsQueryException> FailureOfAsync(byte[] response) =>
        Assert.ThrowsExactlyAsync<DnsQueryException>(async () => await ReadAsync(response));
}
