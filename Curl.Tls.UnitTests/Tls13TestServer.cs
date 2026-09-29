using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An in-memory TLS 1.3 server built from the library's own codecs, key shares, key
/// schedule and signing keys, to drive <see cref="Tls13ClientHandshake" /> through every
/// suite, group and signature scheme without a network. It answers a ClientHello with a
/// HelloRetryRequest when the client did not share <see cref="Group" />, then with the
/// ServerHello and its encrypted flight; it checks the client's flight, certificate
/// included, against its own transcript.
/// </summary>
internal sealed class Tls13TestServer(TestServerCredential credential)
{
    private readonly List<byte[]> transcriptMessages = [];
    private Tls13CipherSuite suite = Tls13CipherSuite.Aes128GcmSha256;
    private byte[] masterSecret = [];
    private bool retried;

    public ushort CipherSuite { get; init; } = Tls13CipherSuite.Aes128GcmSha256.Code;

    public ushort Group { get; init; } = TlsNamedGroup.X25519;

    /// <summary>Gets a value indicating whether the server answers with <see cref="Group" /> even when the client did not share it, instead of retrying.</summary>
    public bool AnswerUnsharedGroup { get; init; }

    public byte[]? RetryCookie { get; init; }

    public string? ApplicationProtocol { get; init; }

    public bool RequestClientCertificate { get; init; }

    public IReadOnlyList<ushort> ClientCertificateSchemes { get; init; } =
        [TlsSignatureScheme.Ed25519, TlsSignatureScheme.EcdsaSecp256r1Sha256, TlsSignatureScheme.RsaPssRsaeSha256];

    public IReadOnlyList<TlsExtension> LeafExtensions { get; init; } = [];

    public byte[] ClientHandshakeTrafficSecret { get; private set; } = [];

    public byte[] ServerHandshakeTrafficSecret { get; private set; } = [];

    public byte[] ClientApplicationTrafficSecret { get; private set; } = [];

    public byte[] ServerApplicationTrafficSecret { get; private set; } = [];

    public List<byte[]> ClientCertificates { get; } = [];

    /// <summary>Answers a ClientHello: either a HelloRetryRequest alone, or the ServerHello and the encrypted flight.</summary>
    public TestServerFlight Answer(byte[] clientHelloBytes)
    {
        ClientHello hello = ClientHello.Decode(Body(clientHelloBytes)).Value;
        IReadOnlyList<KeyShareEntry> offered = KeyShareExtension.DecodeClientShares(Find(hello.Extensions, TlsExtensionType.KeyShare)!).Value;
        suite = Tls13CipherSuite.Find(CipherSuite)!;
        transcriptMessages.Add(clientHelloBytes);
        KeyShareEntry? clientShare = offered.FirstOrDefault(entry => entry.Group == Group);
        if (clientShare is null && !retried && !AnswerUnsharedGroup)
        {
            return RetryRequest(hello);
        }

        using Tls13KeyShare serverShare = SystemTlsRandomSource.Instance.CreateKeyShare(Group);
        byte[] serverHello = new ServerHello(
            0x0303,
            RandomNumberGenerator.GetBytes(32),
            hello.LegacySessionId,
            CipherSuite,
            0,
            [KeyShareExtension.EncodeServerShare(serverShare.Entry), SupportedVersionsExtension.EncodeSelected(0x0304)]).Encode();
        transcriptMessages.Add(serverHello);
        byte[] sharedSecret = clientShare is null ? new byte[32] : serverShare.ComputeSharedSecret(clientShare.KeyExchange)!;
        return new TestServerFlight(serverHello, EncryptedFlight(hello, sharedSecret));
    }

    /// <summary>Checks the client's second flight: its Certificate and CertificateVerify when asked for, then its Finished.</summary>
    public void ReceiveClientFlight(byte[] flight)
    {
        int position = 0;
        if (RequestClientCertificate)
        {
            position += ReceiveClientCertificate(flight);
        }

        HandshakeMessageReadResult finished = HandshakeMessageReader.Read(flight.AsSpan(position));
        Assert.AreEqual(HandshakeType.Finished, finished.Message!.Type);
        CollectionAssert.AreEqual(suite.KeySchedule.ComputeFinishedVerifyData(ClientHandshakeTrafficSecret, TranscriptHash()), finished.Message.Body);
        Assert.AreEqual(flight.Length, position + finished.BytesConsumed);
    }

    private static byte[] Body(byte[] message) => HandshakeMessageReader.Read(message).Message!.Body;

    private static byte[]? Find(IReadOnlyList<TlsExtension> extensions, TlsExtensionType type) =>
        extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private TestServerFlight RetryRequest(ClientHello hello)
    {
        retried = true;
        List<TlsExtension> extensions = [KeyShareExtension.EncodeSelectedGroup(Group), SupportedVersionsExtension.EncodeSelected(0x0304)];
        if (RetryCookie is not null)
        {
            extensions.Add(CookieExtension.Encode(RetryCookie));
        }

        byte[] retry = new ServerHello(0x0303, ServerHello.HelloRetryRequestRandom.ToArray(), hello.LegacySessionId, CipherSuite, 0, extensions).Encode();
        byte[] firstHelloHash = CryptographicOperations.HashData(suite.KeySchedule.HashAlgorithm, transcriptMessages[0]);
        transcriptMessages.Clear();
        transcriptMessages.Add(new HandshakeMessage(HandshakeType.MessageHash, firstHelloHash).Encode());
        transcriptMessages.Add(retry);
        return new TestServerFlight(retry, []);
    }

    private List<byte[]> EncryptedFlight(ClientHello hello, byte[] sharedSecret)
    {
        Tls13KeySchedule schedule = suite.KeySchedule;
        byte[] handshakeSecret = schedule.ComputeHandshakeSecret(schedule.ComputeEarlySecret(null), sharedSecret);
        byte[] serverHelloHash = TranscriptHash();
        ClientHandshakeTrafficSecret = schedule.DeriveClientHandshakeTrafficSecret(handshakeSecret, serverHelloHash);
        ServerHandshakeTrafficSecret = schedule.DeriveServerHandshakeTrafficSecret(handshakeSecret, serverHelloHash);
        masterSecret = schedule.ComputeMasterSecret(handshakeSecret);

        List<byte[]> flight = [];
        Add(flight, new EncryptedExtensions(EncryptedExtensionsFor(hello)).Encode());
        if (RequestClientCertificate)
        {
            Add(flight, new CertificateRequest([], [SignatureAlgorithmsExtension.Encode(ClientCertificateSchemes)]).Encode());
        }

        Add(flight, new CertificateMessage([], [new CertificateEntry(credential.Certificate, LeafExtensions)]).Encode());
        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(true, TranscriptHash());
        Add(flight, new CertificateVerify(credential.Scheme, credential.SigningKey.Sign(credential.Scheme, content)).Encode());
        Add(flight, new Finished(schedule.ComputeFinishedVerifyData(ServerHandshakeTrafficSecret, TranscriptHash())).Encode());
        byte[] serverFinishedHash = TranscriptHash();
        ClientApplicationTrafficSecret = schedule.DeriveClientApplicationTrafficSecret(masterSecret, serverFinishedHash);
        ServerApplicationTrafficSecret = schedule.DeriveServerApplicationTrafficSecret(masterSecret, serverFinishedHash);
        return flight;
    }

    private List<TlsExtension> EncryptedExtensionsFor(ClientHello hello)
    {
        List<TlsExtension> extensions = [];
        if (ApplicationProtocol is not null && Find(hello.Extensions, TlsExtensionType.ApplicationLayerProtocolNegotiation) is not null)
        {
            extensions.Add(ApplicationLayerProtocolNegotiationExtension.Encode([ApplicationProtocol]));
        }

        return extensions;
    }

    private int ReceiveClientCertificate(byte[] flight)
    {
        HandshakeMessageReadResult certificate = HandshakeMessageReader.Read(flight);
        Assert.AreEqual(HandshakeType.Certificate, certificate.Message!.Type);
        CertificateMessage message = CertificateMessage.Decode(certificate.Message.Body).Value;
        Assert.IsEmpty(message.CertificateRequestContext);
        transcriptMessages.Add(flight[..certificate.BytesConsumed]);
        if (message.CertificateList.Count == 0)
        {
            return certificate.BytesConsumed;
        }

        ClientCertificates.AddRange(message.CertificateList.Select(entry => entry.CertificateData));
        HandshakeMessageReadResult verify = HandshakeMessageReader.Read(flight.AsSpan(certificate.BytesConsumed));
        Assert.AreEqual(HandshakeType.CertificateVerify, verify.Message!.Type);
        CertificateVerify signature = CertificateVerify.Decode(verify.Message.Body).Value;
        CollectionAssert.Contains(ClientCertificateSchemes.ToList(), signature.Algorithm);
        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(false, TranscriptHash());
        Assert.IsNull(TlsCertificatePublicKey.Read(ClientCertificates[0])!.VerifySignature(signature.Algorithm, content, signature.Signature));
        transcriptMessages.Add(flight[certificate.BytesConsumed..(certificate.BytesConsumed + verify.BytesConsumed)]);
        return certificate.BytesConsumed + verify.BytesConsumed;
    }

    private void Add(List<byte[]> flight, byte[] message)
    {
        flight.Add(message);
        transcriptMessages.Add(message);
    }

    private byte[] TranscriptHash()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(suite.KeySchedule.HashAlgorithm);
        foreach (byte[] message in transcriptMessages)
        {
            hash.AppendData(message);
        }

        return hash.GetHashAndReset();
    }
}
