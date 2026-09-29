using System.Security.Cryptography;
using Curl.Tls;
using X509CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Quic;

/// <summary>
/// The TLS 1.3 server half of <see cref="QuicTestServer" />, built from <c>Curl.Tls</c>'s own
/// codecs, key schedule and signing key with a generated ECDSA P-256 certificate. It
/// answers a ClientHello with the ServerHello and the encrypted flight (EncryptedExtensions
/// with ALPN and <c>quic_transport_parameters</c>, Certificate, CertificateVerify, Finished)
/// and checks the client's Finished against its own transcript.
/// </summary>
internal sealed class QuicTestTlsServer : IDisposable
{
    private static readonly Lazy<(byte[] Certificate, ECDsa Key)> Credential = new(CreateCredential);

    private readonly List<byte[]> transcript = [];

    private readonly Tls13CipherSuite suite;

    private readonly X25519KeyShare share = new(RandomNumberGenerator.GetBytes(32));

    public QuicTestTlsServer(ushort cipherSuite) => suite = Tls13CipherSuite.Find(cipherSuite)!;

    public Tls13CipherSuite Suite => suite;

    public byte[] ClientHandshakeSecret { get; private set; } = [];

    public byte[] ServerHandshakeSecret { get; private set; } = [];

    public byte[] ClientApplicationSecret { get; private set; } = [];

    public byte[] ServerApplicationSecret { get; private set; } = [];

    /// <summary>Gets the ClientHello the client sent, decoded.</summary>
    public ClientHello? ClientHello { get; private set; }

    /// <summary>Answers a whole ClientHello message with the ServerHello and the encrypted flight.</summary>
    public (byte[] ServerHello, byte[] Flight) Answer(byte[] clientHelloMessage, string? applicationProtocol, byte[]? transportParameters)
    {
        ClientHello = Tls.ClientHello.Decode(HandshakeMessageReader.Read(clientHelloMessage).Message!.Body).Value;
        transcript.Add(clientHelloMessage);
        IReadOnlyList<KeyShareEntry> offered = KeyShareExtension.DecodeClientShares(Find(TlsExtensionType.KeyShare)!).Value;
        KeyShareEntry clientShare = offered.Single(entry => entry.Group == TlsNamedGroup.X25519);
        byte[] serverHello = new ServerHello(
            0x0303,
            RandomNumberGenerator.GetBytes(32),
            ClientHello.LegacySessionId,
            suite.Code,
            0,
            [KeyShareExtension.EncodeServerShare(share.Entry), SupportedVersionsExtension.EncodeSelected(0x0304)]).Encode();
        transcript.Add(serverHello);
        return (serverHello, EncryptedFlight(share.ComputeSharedSecret(clientShare.KeyExchange)!, applicationProtocol, transportParameters));
    }

    /// <summary>Checks the client's Finished.</summary>
    public void ReceiveClientFinished(byte[] message)
    {
        HandshakeMessageReadResult finished = HandshakeMessageReader.Read(message);
        Assert.AreEqual(HandshakeType.Finished, finished.Message!.Type);
        CollectionAssert.AreEqual(suite.KeySchedule.ComputeFinishedVerifyData(ClientHandshakeSecret, TranscriptHash()), finished.Message.Body);
    }

    public void Dispose() => share.Dispose();

    private static (byte[] Certificate, ECDsa Key) CreateCredential()
    {
        ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var certificate = new X509CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return (certificate.RawData, key);
    }

    private byte[]? Find(TlsExtensionType type) => ClientHello!.Extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private byte[] EncryptedFlight(byte[] sharedSecret, string? applicationProtocol, byte[]? transportParameters)
    {
        Tls13KeySchedule schedule = suite.KeySchedule;
        byte[] handshakeSecret = schedule.ComputeHandshakeSecret(schedule.ComputeEarlySecret(null), sharedSecret);
        byte[] serverHelloHash = TranscriptHash();
        ClientHandshakeSecret = schedule.DeriveClientHandshakeTrafficSecret(handshakeSecret, serverHelloHash);
        ServerHandshakeSecret = schedule.DeriveServerHandshakeTrafficSecret(handshakeSecret, serverHelloHash);
        byte[] masterSecret = schedule.ComputeMasterSecret(handshakeSecret);

        List<TlsExtension> extensions = [];
        if (applicationProtocol is not null)
        {
            extensions.Add(ApplicationLayerProtocolNegotiationExtension.Encode([applicationProtocol]));
        }

        if (transportParameters is not null)
        {
            extensions.Add(QuicTransportParametersExtension.Encode(transportParameters));
        }

        List<byte[]> flight = [];
        Add(flight, new EncryptedExtensions(extensions).Encode());
        Add(flight, new CertificateMessage([], [new CertificateEntry(Credential.Value.Certificate, [])]).Encode());
        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(true, TranscriptHash());
        Add(flight, new CertificateVerify(TlsSignatureScheme.EcdsaSecp256r1Sha256, new EcdsaTlsSigningKey(Credential.Value.Key).Sign(TlsSignatureScheme.EcdsaSecp256r1Sha256, content)).Encode());
        Add(flight, new Finished(schedule.ComputeFinishedVerifyData(ServerHandshakeSecret, TranscriptHash())).Encode());
        byte[] serverFinishedHash = TranscriptHash();
        ClientApplicationSecret = schedule.DeriveClientApplicationTrafficSecret(masterSecret, serverFinishedHash);
        ServerApplicationSecret = schedule.DeriveServerApplicationTrafficSecret(masterSecret, serverFinishedHash);
        return [.. flight.SelectMany(message => message)];
    }

    private void Add(List<byte[]> flight, byte[] message)
    {
        flight.Add(message);
        transcript.Add(message);
    }

    private byte[] TranscriptHash()
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(suite.KeySchedule.HashAlgorithm);
        foreach (byte[] message in transcript)
        {
            hash.AppendData(message);
        }

        return hash.GetHashAndReset();
    }
}
