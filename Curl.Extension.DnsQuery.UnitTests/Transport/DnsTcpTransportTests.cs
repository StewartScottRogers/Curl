using Curl.Extension.DnsQuery.Client;
using Curl.Extension.DnsQuery.Transport.Tcp;
using Curl.Extension.DnsQuery.UnitTests.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Transport;

/// <summary>Checks the query over TCP with its two-octet length prefix (RFC 1035 section 4.2.2).</summary>
[TestClass]
public sealed class DnsTcpTransportTests
{
    private static readonly byte[] Query = new DnsWire().Header(0x1234, 0x0100, 1, 0, 0, 0).Question(DnsWire.Name("example.com"), 1, 1).ToArray();
    private static readonly byte[] Answer = DnsWire.SingleAnswer(1, [192, 0, 2, 1]);

    [TestMethod]
    public async Task ExchangeAsync_PrefixesTheQueryWithItsLengthAndReadsTheAnswerByItsLength()
    {
        var server = new ScriptedDnsServer((_, _) => [], [.. DnsWire.UInt16(Answer.Length), .. Answer, 0xEE]) { TcpReadSize = 1 };

        byte[] answer = await new DnsTcpTransport(server).ExchangeAsync("192.0.2.53", 53, Query, ContextOf());

        CollectionAssert.AreEqual(Answer, answer);
        CollectionAssert.AreEqual(new byte[] { 0, (byte)Query.Length }.Concat(Query).ToArray(), server.TcpWritten.ToArray());
        Assert.AreEqual(("tcp", "192.0.2.53", 53), server.Opened.Single());
        Assert.AreEqual(1, server.Disposed);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(10)]
    public async Task ExchangeAsync_ConnectionClosedBeforeTheWholeAnswer_FailsWithExit56(int octetsSent)
    {
        byte[] whole = [.. DnsWire.UInt16(Answer.Length), .. Answer];
        var server = new ScriptedDnsServer((_, _) => [], whole[..octetsSent]);

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsTcpTransport(server).ExchangeAsync("h", 53, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
    }

    [TestMethod]
    public async Task ExchangeAsync_CannotConnect_FailsWithTheConnectorsExitCode()
    {
        var server = new ScriptedDnsServer((_, _) => [], []) { ConnectFailure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to h port 53") };

        DnsQueryException failure = await Assert.ThrowsExactlyAsync<DnsQueryException>(async () =>
            await new DnsTcpTransport(server).ExchangeAsync("h", 53, Query, ContextOf()));

        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreEqual("Failed to connect to h port 53", failure.Message);
    }

    private static TransferContext ContextOf() => new() { Url = CurlUrl.Parse("dns://h/example.com"), Output = new MemoryStream() };
}
