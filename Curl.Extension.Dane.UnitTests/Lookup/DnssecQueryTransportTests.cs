using System.Net;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Lookup;

[TestClass]
public sealed class DnssecQueryTransportTests
{
    private static readonly DnsReply TruncatedAnswer = DnsReply.Empty with { IsTruncatedOverUdp = true };

    [TestMethod]
    public async Task QueryAsync_StrayDatagrams_AreIgnoredUntilTheServersAnswer()
    {
        FakeDnsServer server = new(new ManualTimeProvider());
        server.StrayDatagrams.Add(([0x12], FakeDnsServer.ServerEndPoint));
        server.StrayDatagrams.Add(([0x12], new IPEndPoint(IPAddress.Loopback, 5353)));

        DnssecResponse? response = await Transport(server).QueryAsync("example.test", DnssecRecordType.Tlsa, CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.AreEqual(0, response.ResponseCode);
    }

    [TestMethod]
    public async Task QueryAsync_ChannelCannotOpen_GivesNull()
    {
        FakeDnsServer server = new(new ManualTimeProvider()) { OpenFails = true };

        Assert.IsNull(await Transport(server).QueryAsync("example.test", DnssecRecordType.Tlsa, CancellationToken.None));
    }

    [TestMethod]
    public async Task QueryAsync_TruncatedAndTcpRefused_GivesNull_Rfc7766Section5()
    {
        FakeDnsServer server = new(new ManualTimeProvider()) { ConnectFails = true };
        server.Reply("example.test.", DnssecRecordType.Tlsa, TruncatedAnswer);

        Assert.IsNull(await Transport(server).QueryAsync("example.test", DnssecRecordType.Tlsa, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(10)]
    public async Task QueryAsync_TcpConnectionClosesEarly_GivesNull_Rfc1035Section4_2_2(int octetsSent)
    {
        FakeDnsServer server = new(new ManualTimeProvider()) { TcpReplyLength = octetsSent };
        server.Reply("example.test.", DnssecRecordType.Tlsa, TruncatedAnswer);

        Assert.IsNull(await Transport(server).QueryAsync("example.test", DnssecRecordType.Tlsa, CancellationToken.None));
    }

    [TestMethod]
    public async Task QueryAsync_CallerCancels_Throws()
    {
        FakeDnsServer server = new(new ManualTimeProvider()) { IsSilentOverUdp = true };
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Transport(server).QueryAsync("example.test", DnssecRecordType.Tlsa, cancelled.Token));
    }

    private static DnssecQueryTransport Transport(FakeDnsServer server) =>
        new(server, server, "resolver.test", 53, TimeProvider.System, TimeSpan.FromHours(1));
}
