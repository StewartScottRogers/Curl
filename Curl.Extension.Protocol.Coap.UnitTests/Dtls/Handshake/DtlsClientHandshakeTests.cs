using Curl.Extension.Protocol.Coap.Client;
using Curl.Extension.Protocol.Coap.Dtls.Retransmission;
using Curl.Extension.Protocol.Coap.Dtls.TestServer;
using Curl.Extension.Protocol.Coap.Dtls.Wire;
using Curl.Protocol.Abstractions;
using Curl.Tls;
using TlsContentType = Curl.Tls.TlsContentType;

namespace Curl.Extension.Protocol.Coap.Dtls.Handshake;

[TestClass]
public sealed class DtlsClientHandshakeTests
{
    private const string Host = "dtls.example";

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_Rfc6347Section4_2_FullHandshake_CompletesWithTheServerFinishedVerified(bool ecdsa)
    {
        using DtlsTestServer server = new(ecdsa ? DtlsTestServerKeyKind.Ecdsa : DtlsTestServerKeyKind.Rsa);
        DtlsTestServerChannel channel = new(server);

        using DtlsClientSession session = await RunAsync(channel, insecure: true);

        Assert.IsTrue(server.HandshakeComplete);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_Rfc7627Section4_ServerEchoesExtendedMasterSecretOrNot_CompletesTheHandshake(bool echo)
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa) { EchoExtendedMasterSecret = echo };
        DtlsTestServerChannel channel = new(server);

        using DtlsClientSession session = await RunAsync(channel, insecure: true);

        Assert.IsTrue(server.HandshakeComplete);
    }

    [TestMethod]
    public async Task RunAsync_Rfc6347Section4_2_1_HelloVerifyRequest_ResendsTheClientHelloWithTheCookieAndTheSameRandom()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server);

        using DtlsClientSession session = await RunAsync(channel, insecure: true);

        DtlsHandshakeFragment[] hellos = [.. channel.Sent.Take(2).Select(HandshakeFragmentOf)];
        Assert.AreEqual((ushort)0, hellos[0].Header.MessageSequence);
        Assert.AreEqual((ushort)1, hellos[1].Header.MessageSequence);
        CollectionAssert.AreEqual(DtlsClientHello.Encode(server.ClientRandom, [], Host), hellos[0].Body);
        CollectionAssert.AreEqual(DtlsClientHello.Encode(server.ClientRandom, server.Cookie, Host), hellos[1].Body);
    }

    [TestMethod]
    public async Task RunAsync_Rfc6347Section4_2_1_MalformedHelloVerifyRequest_FailsWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(DtlsHelloVerifyRequest.MessageType, _ => [0xFE]) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_Rfc6347Section4_2_4_LostServerHelloFlight_ResendsTheClientHelloOnTheTimerAndCompletes()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        server.DropNextFlight(DtlsServerFlight.ServerHello);
        DtlsTestServerChannel channel = new(server);

        using DtlsClientSession session = await RunAsync(channel, insecure: true, new StarvedChannelTimeProvider(channel));

        Assert.IsTrue(server.HandshakeComplete);
        Assert.IsGreaterThanOrEqualTo(2, channel.Sent.Count(datagram => IsHandshake(datagram, HandshakeType.ClientHello, 1)));
    }

    [TestMethod]
    public async Task RunAsync_Rfc6347Section4_2_4_LostServerFinishedFlight_ResendsTheKeyExchangeFlightAndCompletes()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        server.DropNextFlight(DtlsServerFlight.Finished);
        DtlsTestServerChannel channel = new(server);

        using DtlsClientSession session = await RunAsync(channel, insecure: true, new StarvedChannelTimeProvider(channel));

        Assert.IsTrue(server.HandshakeComplete);
        Assert.IsGreaterThanOrEqualTo(2, channel.Sent.Count(datagram => IsHandshake(datagram, HandshakeType.ClientKeyExchange, 2)));
    }

    [TestMethod]
    public async Task RunAsync_Rfc6347Section4_2_4_SilentServer_GivesUpAfterTheRetransmissionLimitWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = _ => null };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true, new ImmediateTimeProvider()));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        Assert.HasCount(DtlsFlightRetransmitter.MaximumRetransmissions + 1, channel.Sent);
    }

    [TestMethod]
    public async Task RunAsync_Rfc6347Section4_2_4_ServerFinishedNeverArrives_GivesUpWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server)
        {
            RewriteServerDatagram = datagram => DtlsRecordHeader.Decode(datagram) is { Epoch: 0, ContentType: TlsContentType.Handshake } ? datagram : null,
        };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true, new ImmediateTimeProvider()));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_Rfc5246Section7_4_2_UntrustedChain_FailsWithExit60FromTheProvider()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server);
        TrustSettingTlsProvider provider = new(insecure: false);

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, provider));

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, failure.ExitCode);
        Assert.AreEqual("SSL certificate problem: self-signed certificate", failure.Message);
    }

    [TestMethod]
    public async Task RunAsync_Rfc5246Section7_4_2_UntrustedChainUnderInsecure_IsAcceptedAfterTheProviderSawTheChainAndHost()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server);
        TrustSettingTlsProvider provider = new(insecure: true);

        using DtlsClientSession session = await RunAsync(channel, provider);

        Assert.AreEqual(Host, provider.VerifiedHost);
        Assert.HasCount(1, provider.VerifiedCertificates);
        CollectionAssert.AreEqual(server.CertificateDer, provider.VerifiedCertificates[0].ToArray());
    }

    [TestMethod]
    public async Task RunAsync_Rfc5246Section7_4_1_3_ServerChoosesASuiteNotOffered_FailsWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(HandshakeType.ServerHello, body => [.. body[..35], 0x00, 0x9C, .. body[37..]]) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        StringAssert.Contains(failure.Message, "0x009C");
    }

    [TestMethod]
    public async Task RunAsync_Rfc5246Section7_3_HelloFlightOutOfOrder_FailsWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(HandshakeType.Certificate, body => body, HandshakeType.CertificateRequest) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
    }

    [TestMethod]
    public async Task RunAsync_Rfc5246Section7_4_2_CertificateThatDoesNotParse_FailsWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(HandshakeType.Certificate, _ => [0, 0, 9]) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        StringAssert.Contains(failure.Message, "Certificate does not parse");
    }

    [TestMethod]
    public async Task RunAsync_Rfc5246Section7_4_2_EmptyCertificateList_FailsWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(HandshakeType.Certificate, _ => [0, 0, 0]) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        StringAssert.Contains(failure.Message, "no certificate");
    }

    [TestMethod]
    public async Task RunAsync_Rfc4492Section5_4_ServerChoosesAGroupNotOffered_FailsWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(HandshakeType.ServerKeyExchange, body => [body[0], 0x00, 0x18, .. body[3..]]) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        StringAssert.Contains(failure.Message, "0x0018");
    }

    [TestMethod]
    public async Task RunAsync_Rfc4492Section5_4_ServerPointNotOnTheCurve_FailsWithExit35()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(HandshakeType.ServerKeyExchange, body => [.. body[..5], (byte)(body[5] ^ 1), .. body[6..]]) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        StringAssert.Contains(failure.Message, "not a point");
    }

    [TestMethod]
    public async Task RunAsync_Rfc5246Section7_4_3_KeyExchangeSignatureTampered_FailsWithExit35EvenUnderInsecure()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Rsa);
        DtlsTestServerChannel channel = new(server) { RewriteServerDatagram = RewritingMessage(HandshakeType.ServerKeyExchange, body => [.. body[..^1], (byte)(body[^1] ^ 1)]) };

        CoapException failure = await Assert.ThrowsExactlyAsync<CoapException>(async () => await RunAsync(channel, insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, failure.ExitCode);
        StringAssert.Contains(failure.Message, "signature");
    }

    [TestMethod]
    public async Task RunAsync_Rfc6347Section4_1_2_7_RecordsItCannotUse_AreDroppedAndTheHandshakeCompletes()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        DtlsTestServerChannel channel = new(server);
        channel.Deliver(Record(TlsContentType.Handshake, 1, new byte[24]));
        channel.Deliver(Record(TlsContentType.Alert, 0, [2, 40]));
        channel.Deliver(Record(TlsContentType.Handshake, 0, [1, 2, 3]));
        channel.Deliver([0x16, 0xFE]);

        using DtlsClientSession session = await RunAsync(channel, insecure: true);

        Assert.IsTrue(server.HandshakeComplete);
    }

    internal static async Task<DtlsClientSession> RunAsync(DtlsTestServerChannel channel, bool insecure, TimeProvider? clock = null) =>
        await RunAsync(channel, new TrustSettingTlsProvider(insecure), clock);

    private static async Task<DtlsClientSession> RunAsync(DtlsTestServerChannel channel, ITlsProvider provider, TimeProvider? clock = null)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
        return await new DtlsClientHandshake(channel, provider, Host, clock ?? new SilentTimeProvider()).RunAsync(deadline.Token);
    }

    private static byte[] Record(TlsContentType contentType, ushort epoch, byte[] fragment) =>
        [.. new DtlsRecordHeader(contentType, DtlsRecordHeader.Dtls12Version, epoch, 0, (ushort)fragment.Length).Encode(), .. fragment];

    private static DtlsHandshakeFragment HandshakeFragmentOf(byte[] datagram) =>
        DtlsHandshakeFragment.Decode(datagram.AsSpan(DtlsRecordHeader.HeaderLength), out _);

    private static bool IsHandshake(byte[] datagram, HandshakeType type, ushort messageSequence) =>
        DtlsRecordHeader.Decode(datagram) is { Epoch: 0, ContentType: TlsContentType.Handshake }
        && HandshakeFragmentOf(datagram).Header is { } header
        && header.MessageType == type
        && header.MessageSequence == messageSequence;

    /// <summary>Rewrites the body (and, when given, the type) of the server's unprotected handshake message of <paramref name="type" />.</summary>
    private static Func<byte[], byte[]?> RewritingMessage(HandshakeType type, Func<byte[], byte[]> rewriteBody, HandshakeType? newType = null) => datagram =>
    {
        DtlsRecordHeader header = DtlsRecordHeader.Decode(datagram);
        if (header is not { Epoch: 0, ContentType: TlsContentType.Handshake } || HandshakeFragmentOf(datagram) is not { } fragment || fragment.Header.MessageType != type)
        {
            return datagram;
        }

        byte[] body = rewriteBody(fragment.Body);
        byte[] rewritten = new DtlsHandshakeFragment(new DtlsHandshakeHeader(newType ?? type, body.Length, fragment.Header.MessageSequence, 0, body.Length), body).Encode();
        return [.. (header with { Length = (ushort)rewritten.Length }).Encode(), .. rewritten];
    };
}
