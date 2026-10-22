using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="TcpConnector" /> keeps its DoH, DNS-server and HTTPS-RR behaviour when
/// <c>CURLOPT_DNS_SHUFFLE_ADDRESSES</c> wraps the resolver in a <see cref="ShufflingDnsResolver" /> (BL-3024).
/// </summary>
[TestClass]
public sealed class TcpConnectorShuffledResolverTests
{
    private const int OriginPort = 41443;

    private const string Header = "000001000001000000000000";

    private const string FooExampleCom = "03666F6F076578616D706C6503636F6D00";

    private static readonly Uri DohUrl = new("https://127.0.0.1:41444/dns-query");

    [TestMethod]
    public async Task ConnectAsync_ToAnHttpsOriginThroughAShuffledDohResolver_SendsTheHttpsRecordQuery()
    {
        var httpsQuery = Convert.FromHexString(Header + "065F3431343433065F6874747073" + FooExampleCom + "00410001");
        var queryA = Convert.FromHexString(Header + FooExampleCom + "00010001");
        var queryAaaa = Convert.FromHexString(Header + FooExampleCom + "001C0001");
        var dohConnector = new FakeConnector();
        dohConnector.BytesToRead.AddRange([
            Ok(DnsTestReplies.Answer(httpsQuery, [])),
            Ok(DnsTestReplies.Answer(queryA, [IPAddress.Loopback])),
            Ok(DnsTestReplies.Answer(queryAaaa, [IPAddress.IPv6Loopback])),
        ]);
        var resolver = ShufflingDnsResolver.Over(new DohDnsResolver(dohConnector, DohUrl), shuffleAddresses: true);
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        _ = await connector.ConnectAsync(new ConnectTarget("foo.example.com", OriginPort, UseTls: true) { PoolScheme = "https" }, CancellationToken.None);

        Assert.HasCount(3, dohConnector.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAShuffledDohResolverAnswersNothing_WritesTheSameInfoLinesAsUnshuffled()
    {
        var plain = await FailedDohInfoAsync(shuffleAddresses: false);
        var shuffled = await FailedDohInfoAsync(shuffleAddresses: true);

        CollectionAssert.AreEqual(plain, shuffled);
        Assert.AreEqual(1, shuffled.Count(line => line == "Could not resolve host: example.test"));
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAShuffledDnsServerFoundNoSuchName_FailsFromANegativeDnsEntry()
    {
        var dnsServer = new IPEndPoint(IPAddress.Parse("192.0.2.1"), 53);
        var opener = new ScriptedDnsSocketOpener(new ManualTimeProvider(), (dnsServer, ScriptedDnsServer.AnsweringCode(3)));
        byte nextQueryId = 0;
        var resolver = ShufflingDnsResolver.Over(
            new DnsServerResolver(
                new DnsServerResolverOptions("192.0.2.1", null, null, null), new ManualTimeProvider(), opener, () => [], _ => null,
                destination => destination.Fill(++nextQueryId)),
            shuffleAddresses: true);
        var connector = new TcpConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());
        var second = new RecordingTransferEvents();

        _ = await connector.ConnectAsync(new ConnectTarget("examplehost.invalid", 80, UseTls: false) { Events = new RecordingTransferEvents() }, CancellationToken.None);
        var result = await connector.ConnectAsync(new ConnectTarget("examplehost.invalid", 80, UseTls: false) { Events = second }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.Contains("Negative DNS entry", second.Info);
        Assert.HasCount(2, opener.Sent);
    }

    private static async Task<string[]> FailedDohInfoAsync(bool shuffleAddresses)
    {
        var doh = new DohDnsResolver(new FakeConnector { Failure = ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect") }, DohUrl);
        var events = new RecordingTransferEvents();
        var connector = new TcpConnector(ShufflingDnsResolver.Over(doh, shuffleAddresses), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        _ = await connector.ConnectAsync(new ConnectTarget("example.test", 47113, UseTls: false) { Events = events }, CancellationToken.None);

        return [.. events.Info];
    }

    private static byte[] Ok(byte[] answer) =>
        [.. Encoding.Latin1.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: {answer.Length}\r\n\r\n"), .. answer];
}
