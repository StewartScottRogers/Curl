using static Curl.Tls.Rfc8448Messages;

namespace Curl.Tls;

/// <summary>
/// Replays the handshake messages of RFC 8448 sections 3, 4 and 5 through the codecs: the
/// section 3 ClientHello is built from its fields, and every other message decodes to the
/// values the trace shows and encodes back to the same bytes.
/// </summary>
[TestClass]
public sealed class Rfc8448HandshakeMessageTests
{
    private const string SimpleClientRandom = "cb34ecb1e78163ba1c38c6dacb196a6dffa21a8d9912ec18a2ef6283024dece7";

    private const string SimpleClientKeyShare = "99381de560e4bd43d23d8e435a7dbafeb3c06e51c13cae4d5413691e529aaf2c";

    private const string SimpleServerKeyShare = "c9828876112095fe66762bdbf7c672e156d6cc253b833df1dd69b1b04e751f0f";

    private static readonly ushort[] SimpleGroups = [0x001d, 0x0017, 0x0018, 0x0019, 0x0100, 0x0101, 0x0102, 0x0103, 0x0104];

    private static readonly ushort[] SimpleSignatureAlgorithms =
        [0x0403, 0x0503, 0x0603, 0x0203, 0x0804, 0x0805, 0x0806, 0x0401, 0x0501, 0x0601, 0x0201, 0x0402, 0x0502, 0x0602, 0x0202];

    // RFC 8448 section 3: the ClientHello, from the fields the trace's bytes carry.
    [TestMethod]
    public void EncodeReproducesTheSimpleHandshakeClientHelloFromItsFields()
    {
        ClientHello hello = new(
            0x0303,
            Convert.FromHexString(SimpleClientRandom),
            [],
            [0x1301, 0x1303, 0x1302],
            [0],
            [
                ServerNameExtension.EncodeHostName("server"),
                RenegotiationInfoExtension.Encode([]),
                SupportedGroupsExtension.Encode(SimpleGroups),
                new TlsExtension(TlsExtensionType.SessionTicket, []),
                KeyShareExtension.EncodeClientShares([new KeyShareEntry(0x001d, Convert.FromHexString(SimpleClientKeyShare))]),
                SupportedVersionsExtension.EncodeOffered([0x0304]),
                SignatureAlgorithmsExtension.Encode(SimpleSignatureAlgorithms),
                PskKeyExchangeModesExtension.Encode([1]),
                RecordSizeLimitExtension.Encode(0x4001),
            ]);

        Assert.AreEqual(SimpleClientHello, Convert.ToHexStringLower(hello.Encode()));
    }

    // RFC 8448 section 3: the ClientHello decodes back to its fields.
    [TestMethod]
    public void DecodeReadsTheSimpleHandshakeClientHello()
    {
        ClientHello hello = ClientHello.Decode(Body(SimpleClientHello, HandshakeType.ClientHello)).Value;

        Assert.AreEqual(0x0303, hello.LegacyVersion);
        Assert.AreEqual(SimpleClientRandom, Convert.ToHexStringLower(hello.Random));
        Assert.IsEmpty(hello.LegacySessionId);
        CollectionAssert.AreEqual(new ushort[] { 0x1301, 0x1303, 0x1302 }, hello.CipherSuites.ToArray());
        CollectionAssert.AreEqual(new byte[] { 0 }, hello.LegacyCompressionMethods);
        CollectionAssert.AreEqual(
            new[]
            {
                TlsExtensionType.ServerName, TlsExtensionType.RenegotiationInfo, TlsExtensionType.SupportedGroups,
                TlsExtensionType.SessionTicket, TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions,
                TlsExtensionType.SignatureAlgorithms, TlsExtensionType.PskKeyExchangeModes, TlsExtensionType.RecordSizeLimit,
            },
            hello.Extensions.Select(extension => extension.Type).ToArray());
        Assert.AreEqual("server", ServerNameExtension.DecodeHostName(hello.Extensions[0].Data).Value);
        CollectionAssert.AreEqual(SimpleGroups, SupportedGroupsExtension.Decode(hello.Extensions[2].Data).Value.ToArray());
        KeyShareEntry share = KeyShareExtension.DecodeClientShares(hello.Extensions[4].Data).Value.Single();
        Assert.AreEqual(0x001d, share.Group);
        Assert.AreEqual(SimpleClientKeyShare, Convert.ToHexStringLower(share.KeyExchange));
        CollectionAssert.AreEqual(SimpleSignatureAlgorithms, SignatureAlgorithmsExtension.Decode(hello.Extensions[6].Data).Value.ToArray());
        Assert.AreEqual(SimpleClientHello, Convert.ToHexStringLower(hello.Encode()));
    }

    // RFC 8448 section 3: the ServerHello.
    [TestMethod]
    public void DecodeReadsTheSimpleHandshakeServerHello()
    {
        ServerHello hello = ServerHello.Decode(Body(SimpleServerHello, HandshakeType.ServerHello)).Value;

        Assert.AreEqual(0x0303, hello.LegacyVersion);
        Assert.AreEqual("a6af06a4121860dc5e6e60249cd34c95930c8ac5cb1434dac155772ed3e26928", Convert.ToHexStringLower(hello.Random));
        Assert.IsFalse(hello.IsHelloRetryRequest);
        Assert.IsEmpty(hello.LegacySessionIdEcho);
        Assert.AreEqual(0x1301, hello.CipherSuite);
        Assert.AreEqual(0, hello.LegacyCompressionMethod);
        KeyShareEntry share = KeyShareExtension.DecodeServerShare(hello.Extensions[0].Data).Value;
        Assert.AreEqual(0x001d, share.Group);
        Assert.AreEqual(SimpleServerKeyShare, Convert.ToHexStringLower(share.KeyExchange));
        Assert.AreEqual(0x0304, SupportedVersionsExtension.DecodeSelected(hello.Extensions[1].Data).Value);
        Assert.AreEqual(SimpleServerHello, Convert.ToHexStringLower(hello.Encode()));
    }

    // RFC 8448 section 3: the EncryptedExtensions (supported_groups, record_size_limit, server_name).
    [TestMethod]
    public void DecodeReadsTheSimpleHandshakeEncryptedExtensions()
    {
        EncryptedExtensions message = EncryptedExtensions.Decode(Body(SimpleEncryptedExtensions, HandshakeType.EncryptedExtensions)).Value;

        Assert.HasCount(3, message.Extensions);
        CollectionAssert.AreEqual(SimpleGroups, SupportedGroupsExtension.Decode(message.Extensions[0].Data).Value.ToArray());
        Assert.AreEqual(0x4001, RecordSizeLimitExtension.Decode(message.Extensions[1].Data).Value);
        Assert.AreEqual(TlsExtensionType.ServerName, message.Extensions[2].Type);
        Assert.IsNull(ServerNameExtension.DecodeAcknowledgement(message.Extensions[2].Data));
        Assert.AreEqual(SimpleEncryptedExtensions, Convert.ToHexStringLower(message.Encode()));
    }

    // RFC 8448 section 3: the server's Certificate, one RSA certificate and no entry extensions.
    [TestMethod]
    public void DecodeReadsTheSimpleHandshakeCertificate()
    {
        CertificateMessage message = CertificateMessage.Decode(Body(SimpleCertificate, HandshakeType.Certificate)).Value;

        Assert.IsEmpty(message.CertificateRequestContext);
        CertificateEntry entry = message.CertificateList.Single();
        Assert.HasCount(0x1b0, entry.CertificateData);
        Assert.AreEqual("308201ac", Convert.ToHexStringLower(entry.CertificateData[..4]));
        Assert.IsEmpty(entry.Extensions);
        Assert.AreEqual(SimpleCertificate, Convert.ToHexStringLower(message.Encode()));
    }

    // RFC 8448 section 6: the CertificateRequest, an empty context and signature_algorithms.
    [TestMethod]
    public void DecodeReadsTheClientAuthenticationCertificateRequest()
    {
        CertificateRequest message = CertificateRequest.Decode(Body(ClientAuthenticationCertificateRequest, HandshakeType.CertificateRequest)).Value;

        Assert.IsEmpty(message.CertificateRequestContext);
        Assert.AreEqual(TlsExtensionType.SignatureAlgorithms, message.Extensions.Single().Type);
        CollectionAssert.AreEqual(SimpleSignatureAlgorithms, SignatureAlgorithmsExtension.Decode(message.Extensions[0].Data).Value.ToArray());
        Assert.AreEqual(ClientAuthenticationCertificateRequest, Convert.ToHexStringLower(message.Encode()));
    }

    // RFC 8448 section 3: the CertificateVerify, rsa_pss_rsae_sha256 with a 128-byte signature.
    [TestMethod]
    public void DecodeReadsTheSimpleHandshakeCertificateVerify()
    {
        CertificateVerify message = CertificateVerify.Decode(Body(SimpleCertificateVerify, HandshakeType.CertificateVerify)).Value;

        Assert.AreEqual(0x0804, message.Algorithm);
        Assert.HasCount(128, message.Signature);
        Assert.AreEqual(SimpleCertificateVerify, Convert.ToHexStringLower(message.Encode()));
    }

    // RFC 8448 section 3: the server's and the client's Finished.
    [TestMethod]
    [DataRow(SimpleServerFinished, "9b9b141d906337fbd2cbdce71df4deda4ab42c309572cb7fffee5454b78f0718")]
    [DataRow(SimpleClientFinished, "a8ec436d677634ae525ac1fcebe11a039ec17694fac6e98527b642f2edd5ce61")]
    public void DecodeReadsTheSimpleHandshakeFinished(string encoded, string verifyData)
    {
        Finished message = Finished.Decode(Body(encoded, HandshakeType.Finished)).Value;

        Assert.AreEqual(verifyData, Convert.ToHexStringLower(message.VerifyData));
        Assert.AreEqual(encoded, Convert.ToHexStringLower(message.Encode()));
    }

    // RFC 8448 section 3: the NewSessionTicket, with early_data allowing 1024 bytes.
    [TestMethod]
    public void DecodeReadsTheSimpleHandshakeNewSessionTicket()
    {
        NewSessionTicket message = NewSessionTicket.Decode(Body(SimpleNewSessionTicket, HandshakeType.NewSessionTicket)).Value;

        Assert.AreEqual(30u, message.TicketLifetime);
        Assert.AreEqual(0xfad6aac5u, message.TicketAgeAdd);
        Assert.AreEqual("0000", Convert.ToHexStringLower(message.TicketNonce));
        Assert.HasCount(0xb2, message.Ticket);
        Assert.AreEqual(TlsExtensionType.EarlyData, message.Extensions.Single().Type);
        Assert.AreEqual(1024u, EarlyDataExtension.DecodeMaxEarlyDataSize(message.Extensions[0].Data).Value);
        Assert.AreEqual(SimpleNewSessionTicket, Convert.ToHexStringLower(message.Encode()));
    }

    // RFC 8448 section 4: the resumption ClientHello, with early_data, padding and the
    // ticket offered in pre_shared_key, last.
    [TestMethod]
    public void DecodeReadsTheResumedHandshakeClientHello()
    {
        ClientHello hello = ClientHello.Decode(Body(ResumedClientHello, HandshakeType.ClientHello)).Value;
        NewSessionTicket ticket = NewSessionTicket.Decode(Body(SimpleNewSessionTicket, HandshakeType.NewSessionTicket)).Value;

        Assert.IsNull(EarlyDataExtension.DecodeIndication(hello.Extensions.Single(e => e.Type == TlsExtensionType.EarlyData).Data));
        Assert.AreEqual(87, PaddingExtension.Decode(hello.Extensions.Single(e => e.Type == TlsExtensionType.Padding).Data).Value);
        TlsExtension last = hello.Extensions[^1];
        Assert.AreEqual(TlsExtensionType.PreSharedKey, last.Type);
        OfferedPsks offer = PreSharedKeyExtension.DecodeOffered(last.Data).Value;
        PskIdentity identity = offer.Identities.Single();
        CollectionAssert.AreEqual(ticket.Ticket, identity.Identity);
        Assert.AreEqual(0xfad6aacbu, identity.ObfuscatedTicketAge);
        Assert.AreEqual("3add4fb2d8fdf822a0ca3cf7678ef5e88dae990141c5924d57bb6fa31b9e5f9d", Convert.ToHexStringLower(offer.Binders.Single()));
        Assert.AreEqual(ResumedClientHello, Convert.ToHexStringLower(hello.Encode()));
    }

    // RFC 8448 section 4: the resumption ServerHello selects identity 0.
    [TestMethod]
    public void DecodeReadsTheResumedHandshakeServerHello()
    {
        ServerHello hello = ServerHello.Decode(Body(ResumedServerHello, HandshakeType.ServerHello)).Value;

        Assert.AreEqual(TlsExtensionType.PreSharedKey, hello.Extensions[0].Type);
        Assert.AreEqual(0, PreSharedKeyExtension.DecodeSelected(hello.Extensions[0].Data).Value);
        Assert.AreEqual(ResumedServerHello, Convert.ToHexStringLower(hello.Encode()));
    }

    // RFC 8448 section 5: the HelloRetryRequest asks for secp256r1 and carries a cookie.
    [TestMethod]
    public void DecodeReadsTheHelloRetryRequest()
    {
        ServerHello hello = ServerHello.Decode(Body(HelloRetryRequest, HandshakeType.ServerHello)).Value;

        Assert.IsTrue(hello.IsHelloRetryRequest);
        Assert.AreEqual(0x0017, KeyShareExtension.DecodeSelectedGroup(hello.Extensions[0].Data).Value);
        Assert.AreEqual(TlsExtensionType.Cookie, hello.Extensions[1].Type);
        Assert.HasCount(0x72, CookieExtension.Decode(hello.Extensions[1].Data).Value);
        Assert.AreEqual(0x0304, SupportedVersionsExtension.DecodeSelected(hello.Extensions[2].Data).Value);
        Assert.AreEqual(HelloRetryRequest, Convert.ToHexStringLower(hello.Encode()));
    }
}
