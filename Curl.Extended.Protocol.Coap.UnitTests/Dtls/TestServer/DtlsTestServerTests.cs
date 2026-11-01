using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Extended.Protocol.Coap.Dtls.Record;
using Curl.Extended.Protocol.Coap.Dtls.Wire;
using Curl.Tls;

namespace Curl.Extended.Protocol.Coap.Dtls.TestServer;

[TestClass]
public sealed class DtlsTestServerTests
{
    private static readonly byte[] ClientRandom = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    [TestMethod]
    public void Receive_ClientHelloWithoutCookie_AnswersHelloVerifyRequestWithTheCookie()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);

        IReadOnlyList<byte[]> answers = server.Receive(ClientHelloRecord([], 0, 0));

        Assert.HasCount(1, answers);
        DtlsHandshakeFragment fragment = HandshakeFragmentOf(answers[0]);
        Assert.AreEqual(DtlsHelloVerifyRequest.MessageType, fragment.Header.MessageType);
        CollectionAssert.AreEqual(server.Cookie, DtlsHelloVerifyRequest.Decode(fragment.Body).Cookie);
    }

    [TestMethod]
    public void Receive_ClientHelloWithTheCookie_AnswersTheEcdsaServerFlight()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);

        IReadOnlyList<byte[]> answers = server.Receive(ClientHelloRecord(server.Cookie, 1, 1));

        Assert.HasCount(4, answers);
        HandshakeType[] types = [.. answers.Select(answer => HandshakeFragmentOf(answer).Header.MessageType)];
        CollectionAssert.AreEqual(new[] { HandshakeType.ServerHello, HandshakeType.Certificate, HandshakeType.ServerKeyExchange, HandshakeType.ServerHelloDone }, types);
        ServerHello hello = ServerHello.Decode(HandshakeFragmentOf(answers[0]).Body).Value;
        Assert.AreEqual((ushort)0xC02B, hello.CipherSuite);
        CollectionAssert.AreEqual(server.ServerRandom, hello.Random);
        Tls12CertificateMessage certificate = Tls12CertificateMessage.Decode(HandshakeFragmentOf(answers[1]).Body).Value;
        CollectionAssert.AreEqual(server.CertificateDer, certificate.CertificateList[0]);
    }

    [TestMethod]
    public void Receive_ClientHelloWithTheCookie_SignsTheKeyExchangeWithTheRsaKey()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Rsa);

        IReadOnlyList<byte[]> answers = server.Receive(ClientHelloRecord(server.Cookie, 1, 1));

        Tls12ServerKeyExchange keyExchange = Tls12ServerKeyExchange.Decode(HandshakeFragmentOf(answers[2]).Body, Tls12KeyExchange.Ecdhe, true, true).Value;
        Assert.AreEqual(TlsSignatureScheme.RsaPssRsaeSha256, keyExchange.SignatureAlgorithm);
        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(server.CertificateDer);
        using RSA rsa = certificate.GetRSAPublicKey()!;
        Assert.IsTrue(rsa.VerifyData(keyExchange.BuildSignedContent(ClientRandom, server.ServerRandom), keyExchange.Signature!, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    [TestMethod]
    public void DropNextFlight_ServerHello_SendsNothingOnceThenResendsOnRetransmission()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        server.DropNextFlight(DtlsServerFlight.ServerHello);

        IReadOnlyList<byte[]> dropped = server.Receive(ClientHelloRecord(server.Cookie, 1, 1));
        IReadOnlyList<byte[]> resent = server.Receive(ClientHelloRecord(server.Cookie, 1, 2));

        Assert.IsEmpty(dropped);
        Assert.HasCount(4, resent);
    }

    [TestMethod]
    public void ReplayRecordOfNextFlight_SecondRecord_SendsItTwice()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        server.ReplayRecordOfNextFlight(DtlsServerFlight.ServerHello, 1);

        IReadOnlyList<byte[]> answers = server.Receive(ClientHelloRecord(server.Cookie, 1, 1));

        Assert.HasCount(5, answers);
        CollectionAssert.AreEqual(answers[1], answers[4]);
    }

    [TestMethod]
    public void Receive_CompleteClientHandshake_VerifiesFinishedAnswersFinishedAndEchoesApplicationData()
    {
        using DtlsTestServer server = new(DtlsTestServerKeyKind.Ecdsa);
        List<byte> transcript = [];
        byte[] helloBody = ClientHelloBody(server.Cookie);
        transcript.AddRange(HandshakeFragmentOf(helloBody, 1));
        IReadOnlyList<byte[]> flight = server.Receive(ClientHelloRecord(server.Cookie, 1, 1));
        foreach (byte[] record in flight)
        {
            transcript.AddRange(record[DtlsRecordHeader.HeaderLength..]);
        }

        Tls12ServerKeyExchange serverKeyExchange = Tls12ServerKeyExchange.Decode(HandshakeFragmentOf(flight[2]).Body, Tls12KeyExchange.Ecdhe, true, true).Value;
        using EcdhKeyShare clientShare = EcdhKeyShare.Generate(TlsNamedGroup.Secp256r1);
        byte[] preMasterSecret = clientShare.ComputeSharedSecret(((Tls12EcdheParameters)serverKeyExchange.Parameters).PublicKey)!;
        byte[] masterSecret = TlsPrf.Sha256.ComputeMasterSecret(preMasterSecret, ClientRandom, server.ServerRandom);
        Tls12KeyBlock keys = DtlsKeyBlock.Derive(masterSecret, ClientRandom, server.ServerRandom);
        byte[] keyExchangeBody = new Tls12ClientKeyExchange(Tls12KeyExchange.Ecdhe, [(byte)clientShare.PublicKey.Length, .. clientShare.PublicKey]).Encode()[HandshakeMessage.HeaderLength..];
        byte[] keyExchangeFragment = HandshakeFragmentOf(keyExchangeBody, 2, HandshakeType.ClientKeyExchange);
        transcript.AddRange(keyExchangeFragment);
        byte[] verifyData = TlsPrf.Sha256.ComputeClientVerifyData(masterSecret, SHA256.HashData(transcript.ToArray()));
        byte[] finishedFragment = HandshakeFragmentOf(verifyData, 3, HandshakeType.Finished);
        using DtlsRecordProtector clientProtector = new(keys.ClientWrite, 1);
        using DtlsRecordUnprotector serverUnprotector = new(keys.ServerWrite, 1);

        server.Receive(Epoch0Record(TlsContentType.Handshake, keyExchangeFragment, 2));
        server.Receive(Epoch0Record(TlsContentType.ChangeCipherSpec, [1], 3));
        IReadOnlyList<byte[]> finished = server.Receive(clientProtector.Protect(TlsContentType.Handshake, finishedFragment));
        IReadOnlyList<byte[]> echo = server.Receive(clientProtector.Protect(TlsContentType.ApplicationData, [1, 2, 3]));

        Assert.IsTrue(server.HandshakeComplete);
        Assert.HasCount(2, finished);
        Assert.AreEqual(TlsContentType.ChangeCipherSpec, DtlsRecordHeader.Decode(finished[0]).ContentType);
        Assert.IsTrue(serverUnprotector.TryUnprotect(finished[1], out _, out byte[] serverFinished));
        Assert.AreEqual(HandshakeType.Finished, DtlsHandshakeFragment.Decode(serverFinished, out _).Header.MessageType);
        Assert.HasCount(1, echo);
        Assert.IsTrue(serverUnprotector.TryUnprotect(echo[0], out _, out byte[] echoed));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, echoed);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, server.ReceivedApplicationData[0]);
    }

    private static DtlsHandshakeFragment HandshakeFragmentOf(byte[] record) =>
        DtlsHandshakeFragment.Decode(record.AsSpan(DtlsRecordHeader.HeaderLength), out _);

    private static byte[] ClientHelloBody(byte[] cookie) =>
    [
        0xFE, 0xFD,
        .. ClientRandom,
        0,
        (byte)cookie.Length, .. cookie,
        0, 2, 0xC0, 0x2B,
        1, 0,
    ];

    private static byte[] HandshakeFragmentOf(byte[] body, ushort messageSequence, HandshakeType type = HandshakeType.ClientHello) =>
        new DtlsHandshakeFragment(new DtlsHandshakeHeader(type, body.Length, messageSequence, 0, body.Length), body).Encode();

    private static byte[] Epoch0Record(TlsContentType contentType, byte[] fragment, ulong sequenceNumber) =>
        [.. new DtlsRecordHeader(contentType, DtlsRecordHeader.Dtls12Version, 0, sequenceNumber, (ushort)fragment.Length).Encode(), .. fragment];

    private static byte[] ClientHelloRecord(byte[] cookie, ushort messageSequence, ulong recordSequenceNumber) =>
        Epoch0Record(TlsContentType.Handshake, HandshakeFragmentOf(ClientHelloBody(cookie), messageSequence), recordSequenceNumber);
}
