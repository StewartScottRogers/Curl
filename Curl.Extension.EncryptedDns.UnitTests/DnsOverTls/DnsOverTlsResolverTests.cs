using System.Net;
using Curl.Extension.EncryptedDns.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

/// <summary>
/// Checks <see cref="DnsOverTlsResolver" /> against RFC 7858 over fake connections: port 853 (section 3.1), TLS before
/// any DNS message (section 3.2), length-prefixed messages pipelined and matched by ID (section 3.3), and the A and
/// AAAA answers it hands back.
/// </summary>
[TestClass]
public sealed class DnsOverTlsResolverTests
{
    private static readonly DnsOverTlsServer Server = new("dns.example");

    [TestMethod]
    public async Task ResolveAsync_Rfc7858Section3_1_ConnectsToPort853WithoutTlsOnTheConnector()
    {
        FakeConnector connector = new();

        await Resolver(connector, new FakeTlsProvider()).ResolveAsync("example.com", CancellationToken.None);

        Assert.AreEqual(new ConnectTarget("dns.example", 853, UseTls: false), connector.Targets.Single());
    }

    [TestMethod]
    public async Task ResolveAsync_Rfc7858Section3_2_RunsTheHandshakeOnThePlaintextConnectionBeforeAnyQuery()
    {
        FakeConnector connector = new();
        FakeTlsProvider tls = new();

        await Resolver(connector, tls).ResolveAsync("example.com", CancellationToken.None);

        Assert.AreSame(connector.Plaintext, tls.Plaintext);
        Assert.AreEqual(0, tls.PlaintextBytesWrittenBeforeHandshake);
        Assert.IsEmpty(connector.Plaintext!.Written);
        Assert.AreEqual("dns.example", tls.TargetHost);
    }

    [TestMethod]
    public async Task ResolveAsync_Rfc7858Section3_3_SendsBothQueriesLengthPrefixedOverTls()
    {
        FakeTlsProvider tls = new();

        await Resolver(new FakeConnector(), tls).ResolveAsync("example.com", CancellationToken.None);

        byte[] aaaa = DnsQueryEncoder.Encode(DnsOverTlsResolver.AaaaQueryId, "example.com", DnsRecordType.Aaaa);
        byte[] a = DnsQueryEncoder.Encode(DnsOverTlsResolver.AQueryId, "example.com", DnsRecordType.A);
        CollectionAssert.AreEqual((byte[])[0, (byte)aaaa.Length, .. aaaa, 0, (byte)a.Length, .. a], tls.Secure!.Written.ToArray());
        Assert.AreEqual(1, tls.Secure.FlushCount);
        Assert.IsTrue(tls.Secure.IsDisposed);
    }

    [TestMethod]
    public async Task ResolveAsync_Rfc7858Section3_3_AnswersOutOfOrderInPieces_GivesAaaaAddressesThenA()
    {
        FakeTlsProvider tls = new()
        {
            Secure = new(isSecure: true)
            {
                BytesToRead = [.. Prefixed(AAnswer()), .. Prefixed(AaaaAnswer())],
                ReadChunk = 3,
            },
        };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeConnector(), tls).ResolveAsync("example.com", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { IPAddress.Parse("2001:db8::1"), IPAddress.Parse("192.0.2.1") }, addresses.ToArray());
    }

    [TestMethod]
    public async Task ResolveAsync_AnswerWithAnUnknownIdOrNoId_IsIgnored()
    {
        byte[] unknown = AAnswer();
        unknown[1] = 9;
        FakeTlsProvider tls = new() { Secure = new(isSecure: true) { BytesToRead = [.. Prefixed(unknown), .. Prefixed([7])] } };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeConnector(), tls).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_ServerClosesInsideAMessage_GivesNoAddresses()
    {
        FakeTlsProvider tls = new() { Secure = new(isSecure: true) { BytesToRead = [.. Prefixed(AAnswer())[..10]] } };

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeConnector(), tls).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
    }

    [TestMethod]
    public async Task ResolveAsync_ConnectFails_GivesNoAddressesWithoutAHandshake()
    {
        FakeTlsProvider tls = new();

        IReadOnlyList<IPAddress> addresses = await Resolver(new FakeConnector { Plaintext = null }, tls).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsNull(tls.Plaintext);
    }

    [TestMethod]
    public async Task ResolveAsync_Rfc7858Section3_2_HandshakeFails_SendsNothing()
    {
        FakeConnector connector = new();

        IReadOnlyList<IPAddress> addresses = await Resolver(connector, new FakeTlsProvider { Secure = null }).ResolveAsync("example.com", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsEmpty(connector.Plaintext!.Written);
    }

    [TestMethod]
    [DataRow("192.0.2.7")]
    [DataRow("[2001:db8::7]")]
    public async Task ResolveAsync_AddressLiteral_IsAnsweredWithoutConnecting(string host)
    {
        FakeConnector connector = new();

        IReadOnlyList<IPAddress> addresses = await Resolver(connector, new FakeTlsProvider()).ResolveAsync(host, CancellationToken.None);

        Assert.AreEqual(IPAddress.Parse(host.Trim('[', ']')), addresses.Single());
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_NameRfc1035CannotCarry_GivesNoAddressesWithoutConnecting()
    {
        FakeConnector connector = new();

        IReadOnlyList<IPAddress> addresses = await Resolver(connector, new FakeTlsProvider()).ResolveAsync("a..example", CancellationToken.None);

        Assert.IsEmpty(addresses);
        Assert.IsEmpty(connector.Targets);
    }

    [TestMethod]
    public async Task ResolveAsync_ServerWithPortAndTlsServerName_DialsThePortAndVerifiesTheName()
    {
        FakeConnector connector = new();
        FakeTlsProvider tls = new();
        DnsOverTlsServer server = new("192.0.2.53", 8853, "dns.example");

        await new DnsOverTlsResolver(connector, tls, server).ResolveAsync("example.com", CancellationToken.None);

        Assert.AreEqual(new ConnectTarget("192.0.2.53", 8853, UseTls: false), connector.Targets.Single());
        Assert.AreEqual("dns.example", tls.TargetHost);
    }

    private static DnsOverTlsResolver Resolver(FakeConnector connector, FakeTlsProvider tls) => new(connector, tls, Server);

    private static byte[] Prefixed(byte[] message) => [(byte)(message.Length >> 8), (byte)message.Length, .. message];

    // An answer with no question section and one record whose owner is the root name.
    private static byte[] AAnswer() =>
        [0x00, 0x01, 0x81, 0x80, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x04, 192, 0, 2, 1];

    private static byte[] AaaaAnswer() =>
        [0x00, 0x02, 0x81, 0x80, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x1C, 0x00, 0x01, 0x00, 0x00, 0x00, 0x3C, 0x00, 0x10,
            0x20, 0x01, 0x0D, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01];
}
