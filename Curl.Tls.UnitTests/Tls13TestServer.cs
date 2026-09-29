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

    public ushort CipherSuite { get; init; } = Tls13CipherSuite.Aes128GcmSha256.Code;

    public ushort Group { get; init; } = TlsNamedGroup.X25519;

    /// <summary>Gets a value indicating whether the server answers with <see cref="Group" /> even when the client did not share it, instead of retrying.</summary>
    public bool AnswerUnsharedGroup { get; init; }

    public byte[]? RetryCookie { get; init; }

    public string? ApplicationProtocol { get; init; }

    public bool RequestClientCertificate { get; init; }

    /// <summary>Gets the data of the <c>quic_transport_parameters</c> extension EncryptedExtensions carries, or <see langword="null" /> to send none.</summary>
    public byte[]? QuicTransportParameters { get; init; }

    public IReadOnlyList<ushort> ClientCertificateSchemes { get; init; } =
        [TlsSignatureScheme.Ed25519, TlsSignatureScheme.EcdsaSecp256r1Sha256, TlsSignatureScheme.RsaPssRsaeSha256];

    public IReadOnlyList<TlsExtension> LeafExtensions { get; init; } = [];

    /// <summary>Gets the DER certificates sent after the leaf, issuer first.</summary>
    public IReadOnlyList<byte[]> IssuerCertificates { get; init; } = [];

    /// <summary>Gets what turns the Certificate body into the CompressedCertificate sent in its place (RFC 8879), or <see langword="null" /> to send the Certificate.</summary>
    public Func<byte[], CompressedCertificate>? CompressCertificate { get; init; }

    /// <summary>Gets where the server keeps the tickets it issues and looks up the ones offered, or <see langword="null" /> to issue none and resume nothing.</summary>
    public Tls13TestTicketCache? Tickets { get; init; }

    /// <summary>Gets a value indicating whether the server resumes with a ticket it knows, checking its binder.</summary>
    public bool AcceptResumption { get; init; } = true;

    /// <summary>Gets a value indicating whether a resuming server accepts offered early data (it checks neither suite nor ALPN, so tests can make the client do so).</summary>
    public bool AcceptEarlyData { get; init; } = true;

    /// <summary>Gets the early data limit of the tickets the server issues.</summary>
    public uint MaxEarlyDataSize { get; init; } = 16384;

    /// <summary>Gets the lifetime, in seconds, of the tickets the server issues.</summary>
    public uint TicketLifetime { get; init; } = 7200;

    /// <summary>Gets a value indicating whether the server answered a ClientHello with a HelloRetryRequest.</summary>
    public bool SentHelloRetryRequest { get; private set; }

    public bool IsResumed { get; private set; }

    public bool EarlyDataOffered { get; private set; }

    public bool EarlyDataAccepted { get; private set; }

    public byte[] ClientEarlyTrafficSecret { get; private set; } = [];

    public byte[] ResumptionMasterSecret { get; private set; } = [];

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
        if (clientShare is null && !SentHelloRetryRequest && !AnswerUnsharedGroup)
        {
            return RetryRequest(hello);
        }

        byte[]? preSharedKey = FindResumption(hello, clientHelloBytes);
        (byte[] serverKeyExchange, byte[] sharedSecret) = ServerShare(clientShare);
        List<TlsExtension> extensions = [KeyShareExtension.EncodeServerShare(new KeyShareEntry(Group, serverKeyExchange)), SupportedVersionsExtension.EncodeSelected(0x0304)];
        if (preSharedKey is not null)
        {
            extensions.Add(PreSharedKeyExtension.EncodeSelected(0));
        }

        byte[] serverHello = new ServerHello(0x0303, RandomNumberGenerator.GetBytes(32), hello.LegacySessionId, CipherSuite, 0, extensions).Encode();
        transcriptMessages.Add(serverHello);
        return new TestServerFlight(serverHello, EncryptedFlight(hello, sharedSecret, preSharedKey));
    }

    /// <summary>
    /// The server's <c>key_share</c> value and the shared secret: a share of its own that
    /// agrees with the client's, or for X25519MLKEM768 an encapsulation to the client's
    /// share; with no client share, a value the client cannot use and an all-zero secret.
    /// </summary>
    private (byte[] KeyExchange, byte[] SharedSecret) ServerShare(KeyShareEntry? clientShare)
    {
        if (Group == TlsNamedGroup.X25519MlKem768)
        {
            return X25519MlKem768ServerShare.Answer(clientShare?.KeyExchange);
        }

        using Tls13KeyShare serverShare = SystemTlsRandomSource.Instance.CreateKeyShare(Group);
        return (serverShare.PublicKey, clientShare is null ? new byte[32] : serverShare.ComputeSharedSecret(clientShare.KeyExchange)!);
    }

    /// <summary>Checks the client's EndOfEarlyData, sent under the early keys after accepted early data.</summary>
    public void ReceiveEndOfEarlyData(byte[] message)
    {
        Assert.IsTrue(EarlyDataAccepted);
        CollectionAssert.AreEqual(new byte[] { 5, 0, 0, 0 }, message);
        transcriptMessages.Add(message);
    }

    /// <summary>Checks the client's second flight: its Certificate and CertificateVerify when asked for, then its Finished.</summary>
    public void ReceiveClientFlight(byte[] flight)
    {
        transcriptMessages.Add(ReceiveClientAuthentication(flight, [], ClientHandshakeTrafficSecret, RequestClientCertificate));
        ResumptionMasterSecret = suite.KeySchedule.DeriveResumptionMasterSecret(masterSecret, TranscriptHash());
    }

    /// <summary>Returns a NewSessionTicket with a fresh ticket and nonce, keeping its PSK in <see cref="Tickets" />.</summary>
    public byte[] IssueTicket(IReadOnlyList<TlsExtension>? extensions = null)
    {
        byte[] ticket = RandomNumberGenerator.GetBytes(48);
        byte[] nonce = RandomNumberGenerator.GetBytes(8);
        Tickets!.Add(ticket, suite.KeySchedule.DeriveResumptionPreSharedKey(ResumptionMasterSecret, nonce));
        uint ageAdd = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
        return new NewSessionTicket(TicketLifetime, ageAdd, nonce, ticket, extensions ?? [EarlyDataExtension.EncodeMaxEarlyDataSize(MaxEarlyDataSize)]).Encode();
    }

    /// <summary>Returns a post-handshake CertificateRequest (RFC 8446 section 4.6.2) naming <paramref name="context" /> and <see cref="ClientCertificateSchemes" />.</summary>
    public byte[] CreatePostHandshakeCertificateRequest(byte[] context) =>
        new CertificateRequest(context, [SignatureAlgorithmsExtension.Encode(ClientCertificateSchemes)]).Encode();

    /// <summary>
    /// Checks the client's answer to a post-handshake CertificateRequest: Certificate echoing
    /// <paramref name="context" />, CertificateVerify when it holds a certificate, and Finished
    /// keyed from <paramref name="clientApplicationTrafficSecret" />, all over the handshake's
    /// transcript plus the request, which is left as it was.
    /// </summary>
    public void ReceivePostHandshakeAnswer(byte[] request, byte[] answer, byte[] context, byte[] clientApplicationTrafficSecret)
    {
        int handshakeMessages = transcriptMessages.Count;
        transcriptMessages.Add(request);
        ReceiveClientAuthentication(answer, context, clientApplicationTrafficSecret, true);
        transcriptMessages.RemoveRange(handshakeMessages, transcriptMessages.Count - handshakeMessages);
    }

    private static byte[] Body(byte[] message) => HandshakeMessageReader.Read(message).Message!.Body;

    private static byte[]? Find(IReadOnlyList<TlsExtension> extensions, TlsExtensionType type) =>
        extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private TestServerFlight RetryRequest(ClientHello hello)
    {
        SentHelloRetryRequest = true;
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

    /// <summary>
    /// Returns the PSK of the ticket the ClientHello offers when the server knows it and
    /// resumes, after checking the binder over the transcript so far and the hello up to its
    /// binders; notes whether early data was offered and is accepted, and its secret.
    /// </summary>
    private byte[]? FindResumption(ClientHello hello, byte[] clientHelloBytes)
    {
        byte[]? data = Find(hello.Extensions, TlsExtensionType.PreSharedKey);
        EarlyDataOffered = Find(hello.Extensions, TlsExtensionType.EarlyData) is not null;
        byte[]? preSharedKey = data is null ? null : Tickets?.Find(PreSharedKeyExtension.DecodeOffered(data).Value.Identities[0].Identity);
        if (preSharedKey is null || !AcceptResumption)
        {
            return null;
        }

        Tls13KeySchedule schedule = suite.KeySchedule;
        byte[] earlySecret = schedule.ComputeEarlySecret(preSharedKey);
        byte[] binder = PreSharedKeyExtension.DecodeOffered(data!).Value.Binders[0];
        List<byte[]> prefix = [.. transcriptMessages.SkipLast(1), clientHelloBytes[..^(3 + binder.Length)]];
        CollectionAssert.AreEqual(schedule.ComputePskBinder(schedule.DeriveResumptionBinderKey(earlySecret), Hash(prefix)), binder);
        IsResumed = true;
        EarlyDataAccepted = EarlyDataOffered && AcceptEarlyData;
        ClientEarlyTrafficSecret = schedule.DeriveClientEarlyTrafficSecret(earlySecret, TranscriptHash());
        return preSharedKey;
    }

    private List<byte[]> EncryptedFlight(ClientHello hello, byte[] sharedSecret, byte[]? preSharedKey)
    {
        Tls13KeySchedule schedule = suite.KeySchedule;
        byte[] handshakeSecret = schedule.ComputeHandshakeSecret(schedule.ComputeEarlySecret(preSharedKey), sharedSecret);
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

        if (preSharedKey is null)
        {
            CertificateEntry[] chain = [new CertificateEntry(credential.Certificate, LeafExtensions), .. IssuerCertificates.Select(issuer => new CertificateEntry(issuer, []))];
            byte[] certificate = new CertificateMessage([], chain).Encode();
            Add(flight, CompressCertificate is null ? certificate : CompressCertificate(certificate[HandshakeMessage.HeaderLength..]).Encode());
            byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(true, TranscriptHash());
            Add(flight, new CertificateVerify(credential.Scheme, credential.SigningKey.Sign(credential.Scheme, content)).Encode());
        }

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

        if (QuicTransportParameters is not null)
        {
            extensions.Add(QuicTransportParametersExtension.Encode(QuicTransportParameters));
        }

        if (EarlyDataAccepted)
        {
            extensions.Add(EarlyDataExtension.EncodeIndication());
        }

        return extensions;
    }

    /// <summary>Checks a Certificate (and CertificateVerify) when <paramref name="certificateAsked" />, then the Finished ending <paramref name="flight" />, and returns the Finished.</summary>
    private byte[] ReceiveClientAuthentication(byte[] flight, byte[] context, byte[] finishedBaseKey, bool certificateAsked)
    {
        int position = 0;
        if (certificateAsked)
        {
            position += ReceiveClientCertificate(flight, context);
        }

        HandshakeMessageReadResult finished = HandshakeMessageReader.Read(flight.AsSpan(position));
        Assert.AreEqual(HandshakeType.Finished, finished.Message!.Type);
        CollectionAssert.AreEqual(suite.KeySchedule.ComputeFinishedVerifyData(finishedBaseKey, TranscriptHash()), finished.Message.Body);
        Assert.AreEqual(flight.Length, position + finished.BytesConsumed);
        return flight[position..];
    }

    private int ReceiveClientCertificate(byte[] flight, byte[] context)
    {
        HandshakeMessageReadResult certificate = HandshakeMessageReader.Read(flight);
        Assert.AreEqual(HandshakeType.Certificate, certificate.Message!.Type);
        CertificateMessage message = CertificateMessage.Decode(certificate.Message.Body).Value;
        CollectionAssert.AreEqual(context, message.CertificateRequestContext);
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

    private byte[] TranscriptHash() => Hash(transcriptMessages);

    private byte[] Hash(List<byte[]> messages)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(suite.KeySchedule.HashAlgorithm);
        foreach (byte[] message in messages)
        {
            hash.AppendData(message);
        }

        return hash.GetHashAndReset();
    }
}
