namespace Curl.Kerberos;

/// <summary>
/// Checks that <see cref="KerberosKdcSender" /> reaches a realm's KDCs as RFC 4120 section 7.2
/// says: UDP for a request within <c>udp_preference_limit</c>, TCP with a four-byte length
/// prefix otherwise or after <c>KRB_ERR_RESPONSE_TOO_BIG</c>, a <c>KDC-PROXY-MESSAGE</c> for an
/// HTTPS (MS-KKDCP) KDC, and the next KDC when one does not answer.
/// </summary>
[TestClass]
public sealed class KerberosKdcSenderTests
{
    private const string OneKdc = "[realms]\n EXAMPLE.TEST = {\n kdc = kdc.example.test\n }\n";

    private const string HttpsThenTcp = "[realms]\n EXAMPLE.TEST = {\n kdc = https://proxy.example.test/KdcProxy\n kdc = tcp/kdc.example.test\n }\n";

    private static readonly byte[] Reply = FakeKdc.Error(6);

    [TestMethod]
    public async Task SendAsync_RequestWithinUdpLimit_ExchangesOneDatagram()
    {
        FakeKdc kdc = Answering(Reply);

        byte[] reply = await SenderFor(kdc, OneKdc).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(Reply, reply);
        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88" }, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_RequestLongerThanUdpLimit_SendsItOverTcpWithALengthPrefix()
    {
        FakeKdc kdc = Answering(Reply);
        string configuration = "[libdefaults]\n udp_preference_limit = 1\n" + OneKdc;

        byte[] reply = await SenderFor(kdc, configuration).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(Reply, reply);
        CollectionAssert.AreEqual(new[] { "tcp kdc.example.test:88" }, kdc.Exchanges);
        Assert.HasCount(1, kdc.Requests);
        Assert.IsTrue(kdc.Streams.Single().WasDisposed);
    }

    [TestMethod]
    public async Task SendAsync_UdpReplyIsResponseTooBig_AsksTheSameKdcOverTcp()
    {
        FakeKdc kdc = Answering(Reply);
        kdc.AnswersUdpTooBig = true;

        byte[] reply = await SenderFor(kdc, OneKdc).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(Reply, reply);
        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:88", "tcp kdc.example.test:88" }, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_UdpReplyIsAnotherError_ReturnsItWithoutTcp()
    {
        byte[] preAuthenticationRequired = FakeKdc.Error(KerberosErrorMessage.PreAuthenticationRequired);
        FakeKdc kdc = Answering(preAuthenticationRequired);

        byte[] reply = await SenderFor(kdc, OneKdc).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(preAuthenticationRequired, reply);
        Assert.HasCount(1, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_UdpReplyIsNotAMessage_ReturnsItForTheCallerToRefuse()
    {
        byte[] garbage = [0x04, 0x01, 0x00];
        FakeKdc kdc = Answering(garbage);

        byte[] reply = await SenderFor(kdc, OneKdc).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(garbage, reply);
        Assert.HasCount(1, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_UdpOnlyEntry_KeepsToUdpForALongRequest()
    {
        FakeKdc kdc = Answering(Reply);
        string configuration = "[libdefaults]\n udp_preference_limit = 1\n[realms]\n EXAMPLE.TEST = {\n kdc = udp/kdc.example.test:750\n }\n";

        await SenderFor(kdc, configuration).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "udp kdc.example.test:750" }, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_HttpsEntrySkippedAndTcpOnlyEntry_UsesTcp()
    {
        FakeKdc kdc = Answering(Reply);
        string configuration = "[realms]\n EXAMPLE.TEST = {\n kdc = https://proxy.example.test/KdcProxy\n kdc = tcp/kdc.example.test\n }\n";

        await SenderFor(kdc, configuration).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "tcp kdc.example.test:88" }, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_HttpsEntryWithAProxyTransport_PostsAKdcProxyMessageAndUnwrapsItsReply()
    {
        FakeKdc kdc = Answering(Reply);
        byte[] request = Request();

        byte[] reply = await ProxiedSenderFor(kdc, HttpsThenTcp).SendAsync(FakeKdc.Realm, request, CancellationToken.None);

        CollectionAssert.AreEqual(Reply, reply);
        CollectionAssert.AreEqual(new[] { "https proxy.example.test:443/KdcProxy" }, kdc.Exchanges);
        CollectionAssert.AreEqual(new KerberosKdcProxyMessage(request, FakeKdc.Realm).Encode(), kdc.ProxyBodies.Single());
        Assert.AreEqual(KerberosMessageType.AsRequest, kdc.Requests.Single().MessageType);
    }

    [TestMethod]
    public async Task SendAsync_HttpsEntryInARealmWithHttpAnchors_PostsVerifiedAgainstThem()
    {
        FakeKdc kdc = Answering(Reply);
        string configuration = "[realms]\n EXAMPLE.TEST = {\n kdc = https://proxy.example.test/KdcProxy\n http_anchors = FILE:/etc/proxy-ca.pem\n http_anchors = ENV:PROXY_CA\n }\n";

        await ProxiedSenderFor(kdc, configuration).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "FILE:/etc/proxy-ca.pem", "ENV:PROXY_CA" }, kdc.ProxyAnchors.Single().ToArray());
    }

    [TestMethod]
    public async Task SendAsync_HttpsEntryInARealmWithoutHttpAnchors_PostsWithNoAnchors()
    {
        FakeKdc kdc = Answering(Reply);

        await ProxiedSenderFor(kdc, HttpsThenTcp).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        Assert.IsEmpty(kdc.ProxyAnchors.Single());
    }

    [TestMethod]
    public async Task SendAsync_ProxyReplyIsNotAKdcProxyMessage_TriesTheNextKdc()
    {
        FakeKdc kdc = Answering(Reply);
        kdc.ProxyReply = [0x04, 0x01, 0x00];

        byte[] reply = await ProxiedSenderFor(kdc, HttpsThenTcp).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(Reply, reply);
        CollectionAssert.AreEqual(new[] { "https proxy.example.test:443/KdcProxy", "tcp kdc.example.test:88" }, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_ProxyDoesNotAnswer_TriesTheNextKdc()
    {
        FakeKdc kdc = Answering(Reply);
        kdc.UnreachableHosts.Add("proxy.example.test");

        byte[] reply = await ProxiedSenderFor(kdc, HttpsThenTcp).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(Reply, reply);
        CollectionAssert.AreEqual(new[] { "https proxy.example.test:443/KdcProxy", "tcp kdc.example.test:88" }, kdc.Exchanges);
    }

    [TestMethod]
    [DataRow("[libdefaults]\n dns_lookup_kdc = false\n")]
    [DataRow("[realms]\n EXAMPLE.TEST = {\n kdc = https://proxy.example.test\n }\n")]
    public async Task SendAsync_NoUdpOrTcpKdc_ThrowsNoKdc(string configuration)
    {
        FakeKdc kdc = Answering(Reply);

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => SenderFor(kdc, configuration).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.NoKdc, failure.Error);
        Assert.IsEmpty(kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_FirstKdcDoesNotAnswer_TriesTheNext()
    {
        FakeKdc kdc = Answering(Reply);
        kdc.UnreachableHosts.Add("kdc1.example.test");
        string configuration = "[realms]\n EXAMPLE.TEST = {\n kdc = kdc1.example.test\n kdc = kdc2.example.test\n }\n";

        byte[] reply = await SenderFor(kdc, configuration).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None);

        CollectionAssert.AreEqual(Reply, reply);
        CollectionAssert.AreEqual(new[] { "udp kdc1.example.test:88", "udp kdc2.example.test:88" }, kdc.Exchanges);
    }

    [TestMethod]
    public async Task SendAsync_NoKdcAnswers_ThrowsKdcUnreachable()
    {
        FakeKdc kdc = Answering(Reply);
        kdc.UnreachableHosts.Add("kdc.example.test");

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => SenderFor(kdc, OneKdc).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.KdcUnreachable, failure.Error);
    }

    [TestMethod]
    public async Task SendAsync_TcpReplyLongerThanTheLimit_ThrowsUnexpectedReply()
    {
        FakeKdc kdc = Answering(Reply);
        kdc.AnswersUdpTooBig = true;
        kdc.TcpReplyLengthPrefix = KerberosKdcSender.MaximumTcpReplyLength + 1;

        KerberosKdcException failure = await Assert.ThrowsExactlyAsync<KerberosKdcException>(
            () => SenderFor(kdc, OneKdc).SendAsync(FakeKdc.Realm, Request(), CancellationToken.None));

        Assert.AreEqual(KerberosKdcError.UnexpectedReply, failure.Error);
    }

    private static FakeKdc Answering(byte[] reply) => new() { Override = _ => reply };

    private static KerberosKdcSender ProxiedSenderFor(FakeKdc kdc, string configuration)
    {
        KerberosConfiguration parsed = Parse(configuration);
        return new KerberosKdcSender(parsed, new KerberosKdcLocator(parsed, new FakeSrvLookup()), kdc, kdc);
    }

    private static KerberosKdcSender SenderFor(FakeKdc kdc, string configuration)
    {
        KerberosConfiguration parsed = Parse(configuration);
        return new KerberosKdcSender(parsed, new KerberosKdcLocator(parsed, new FakeSrvLookup()), kdc);
    }

    private static byte[] Request() => new KerberosKdcRequest
    {
        MessageType = KerberosMessageType.AsRequest,
        Body = new KerberosKdcRequestBody
        {
            Options = KerberosKdcOptions.None,
            ClientName = new KerberosPrincipalName(1, ["alice"]),
            Realm = FakeKdc.Realm,
            ServerName = new KerberosPrincipalName(2, ["krbtgt", FakeKdc.Realm]),
            Till = FakeKdc.Now,
            Nonce = 1,
            EncryptionTypes = [18],
        },
    }.Encode();

    private static KerberosConfiguration Parse(string text)
    {
        KerberosConfigurationNode root = new(string.Empty, null);
        new KerberosConfigurationReader(new InMemoryKerberosFiles()).Parse(text, "test.conf", root);
        return new KerberosConfiguration(root);
    }
}
