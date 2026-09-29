using System.Numerics;
using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Tls;

/// <summary>
/// An in-memory TLS 1.2, 1.1 and 1.0 server built from the library's own codecs, key
/// shares, PRF and signing keys, to drive <see cref="Tls12ClientHandshake" /> through every
/// key exchange, suite family, version and resumption without a network. It answers a
/// ClientHello with its full flight, or with the abbreviated one when <see cref="Sessions" />
/// knows the offered session ID or ticket; it checks the client's flight (certificate,
/// CertificateVerify and Finished included) against its own transcript and derives the
/// same master secret and key block.
/// </summary>
/// <param name="credential">The server's certificate and key, or <see langword="null" /> for an anonymous suite.</param>
internal sealed class Tls12TestServer(TestServerCredential? credential)
{
    private readonly List<byte> transcript = [];
    private ClientHello hello = null!;
    private byte[] serverRandom = [];
    private byte[] sessionId = [];
    private bool extendedMasterSecret;
    private bool encryptThenMac;
    private bool resumed;
    private Tls13KeyShare? ecdheShare;
    private FiniteFieldDiffieHellman? dheKey;

    public TlsProtocolVersion Version { get; init; } = TlsProtocolVersion.Tls12;

    public ushort CipherSuite { get; init; } = 0xc02b;

    public ushort EcdheGroup { get; init; } = TlsNamedGroup.X25519;

    public FiniteFieldDiffieHellmanGroup DheGroup { get; init; } = FiniteFieldDiffieHellmanGroup.Ffdhe2048;

    /// <summary>Gets the TLS 1.2 scheme the ServerKeyExchange is signed with; the credential's own when not set.</summary>
    public ushort? SignatureScheme { get; init; }

    public string? ApplicationProtocol { get; init; }

    public byte[]? OcspResponse { get; init; }

    /// <summary>Gets a value indicating whether the server echoes <c>status_request</c> but sends no CertificateStatus.</summary>
    public bool OmitCertificateStatus { get; init; }

    /// <summary>Gets the DER certificates sent after the leaf, issuer first.</summary>
    public IReadOnlyList<byte[]> IssuerCertificates { get; init; } = [];

    public bool RequestClientCertificate { get; init; }

    public IReadOnlyList<ushort> ClientCertificateSchemes { get; init; } =
        [TlsSignatureScheme.Ed25519, TlsSignatureScheme.EcdsaSecp256r1Sha256, TlsSignatureScheme.RsaPkcs1Sha256];

    public bool IssueTicket { get; init; }

    public bool SendRenegotiationInfo { get; init; } = true;

    public bool EchoExtendedMasterSecret { get; init; } = true;

    public bool EchoEncryptThenMac { get; init; } = true;

    /// <summary>Gets a value indicating whether the ServerHello random ends in the TLS 1.1-and-below downgrade sentinel.</summary>
    public bool SendDowngradeSentinel { get; init; }

    /// <summary>Gets a value indicating whether the ServerHello random ends in the sentinel a TLS 1.3 server sends when it negotiates TLS 1.2.</summary>
    public bool SendTls12DowngradeSentinel { get; init; }

    public Tls12TestSessionCache Sessions { get; init; } = new();

    public byte[] MasterSecret { get; private set; } = [];

    public Tls12KeyBlock KeyBlock { get; private set; } = null!;

    /// <summary>Gets the record protection the key block was derived for.</summary>
    public Tls12RecordProtectionParameters RecordProtection { get; private set; } = null!;

    public List<byte[]> ClientCertificates { get; } = [];

    private Tls12CipherSuite Suite => Tls12CipherSuite.Find(CipherSuite)!;

    private TlsPrf Prf => Suite.PrfFor(Version);

    /// <summary>Answers a ClientHello with the full flight up to ServerHelloDone, or with the abbreviated one when it resumes.</summary>
    public List<Tls12OutgoingMessage> Answer(byte[] clientHelloBytes)
    {
        transcript.Clear();
        transcript.AddRange(clientHelloBytes);
        hello = ClientHello.Decode(HandshakeMessageReader.Read(clientHelloBytes).Message!.Body).Value;
        serverRandom = RandomNumberGenerator.GetBytes(32);
        if (SendDowngradeSentinel)
        {
            "DOWNGRD\0"u8.CopyTo(serverRandom.AsSpan(24));
        }

        if (SendTls12DowngradeSentinel)
        {
            "DOWNGRD\u0001"u8.CopyTo(serverRandom.AsSpan(24));
        }

        Tls12TestSession? session = FindSession();
        resumed = session is not null;
        sessionId = resumed ? hello.LegacySessionId : RandomNumberGenerator.GetBytes(32);
        extendedMasterSecret = session?.ExtendedMasterSecret ?? (Offered(TlsExtensionType.ExtendedMasterSecret) && EchoExtendedMasterSecret);
        encryptThenMac = Offered(TlsExtensionType.EncryptThenMac) && EchoEncryptThenMac;
        List<Tls12OutgoingMessage> flight = [];
        Add(flight, new ServerHello((ushort)Version, serverRandom, sessionId, CipherSuite, 0, ServerHelloExtensions()).Encode());
        if (session is not null)
        {
            MasterSecret = session.MasterSecret;
            DeriveKeyBlock();
            AddTicketChangeCipherSpecAndFinished(flight);
            return flight;
        }

        AddFullFlight(flight);
        return flight;
    }

    /// <summary>Checks the client's flight and answers with the server's ChangeCipherSpec and Finished, or checks the client's last Finished of a resumption.</summary>
    public List<Tls12OutgoingMessage> ReceiveClientFlight(IReadOnlyList<Tls12OutgoingMessage> flight)
    {
        int changeCipherSpec = flight.ToList().FindIndex(message => message.ContentType == TlsContentType.ChangeCipherSpec);
        ReceiveClientKeyExchange([.. flight.Take(changeCipherSpec)]);
        return ReceiveClientFinished([.. flight.Skip(changeCipherSpec)]);
    }

    /// <summary>Checks the client's flight up to its ChangeCipherSpec and derives the key block; a resumption sends nothing before it.</summary>
    public void ReceiveClientKeyExchange(IReadOnlyList<Tls12OutgoingMessage> flight)
    {
        if (resumed)
        {
            return;
        }

        Queue<Tls12OutgoingMessage> messages = new(flight);
        if (RequestClientCertificate)
        {
            ReceiveClientCertificate(messages.Dequeue().Bytes);
        }

        byte[] clientKeyExchange = Take(messages, HandshakeType.ClientKeyExchange);
        MasterSecret = ComputeMasterSecret(Tls12ClientKeyExchange.Decode(Body(clientKeyExchange), Suite.KeyExchange).Value.ExchangeKeys);
        if (ClientCertificates.Count > 0)
        {
            ReceiveCertificateVerify(messages.Dequeue().Bytes);
        }

        DeriveKeyBlock();
    }

    /// <summary>Checks the client's ChangeCipherSpec and Finished and answers with the server's, or with nothing after a resumption.</summary>
    public List<Tls12OutgoingMessage> ReceiveClientFinished(IReadOnlyList<Tls12OutgoingMessage> flight)
    {
        Queue<Tls12OutgoingMessage> messages = new(flight);
        ReceiveChangeCipherSpecAndFinished(messages);
        if (resumed)
        {
            return [];
        }

        Sessions.Add(sessionId, new Tls12TestSession(MasterSecret, extendedMasterSecret));
        List<Tls12OutgoingMessage> answer = [];
        AddTicketChangeCipherSpecAndFinished(answer);
        return answer;
    }

    /// <summary>Signs as TLS 1.0 and 1.1 do with RSA: the MD5 and SHA-1 block, raised to the private exponent.</summary>
    public static byte[] SignMd5Sha1(RSA key, byte[] content)
    {
        RSAParameters parameters = key.ExportParameters(true);
        byte[] block = TlsSignatureScheme.BuildMd5Sha1Block(content, parameters.Modulus!.Length);
        BigInteger signature = BigInteger.ModPow(
            new BigInteger(block, isUnsigned: true, isBigEndian: true),
            new BigInteger(parameters.D, isUnsigned: true, isBigEndian: true),
            new BigInteger(parameters.Modulus, isUnsigned: true, isBigEndian: true));
        byte[] padded = new byte[parameters.Modulus.Length];
        byte[] value = signature.ToByteArray(isUnsigned: true, isBigEndian: true);
        value.CopyTo(padded, padded.Length - value.Length);
        return padded;
    }

    private static byte[] Body(byte[] message) => HandshakeMessageReader.Read(message).Message!.Body;

    private static Tls12OutgoingMessage Handshake(byte[] message) => new(TlsContentType.Handshake, message);

    private byte[] Take(Queue<Tls12OutgoingMessage> messages, HandshakeType type)
    {
        byte[] message = messages.Dequeue().Bytes;
        Assert.AreEqual(type, (HandshakeType)message[0]);
        transcript.AddRange(message);
        return message;
    }

    private bool Offered(TlsExtensionType type) => hello.Extensions.Any(extension => extension.Type == type);

    private byte[]? OfferedData(TlsExtensionType type) => hello.Extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private Tls12TestSession? FindSession()
    {
        byte[]? ticket = OfferedData(TlsExtensionType.SessionTicket);
        return ticket is { Length: > 0 } ? Sessions.FindByTicket(ticket) : Sessions.FindById(hello.LegacySessionId);
    }

    private List<TlsExtension> ServerHelloExtensions()
    {
        List<TlsExtension> extensions = [];
        if (SendRenegotiationInfo)
        {
            extensions.Add(RenegotiationInfoExtension.Encode([]));
        }

        AddEcho(extensions, extendedMasterSecret, TlsExtensionType.ExtendedMasterSecret);
        AddEcho(extensions, encryptThenMac, TlsExtensionType.EncryptThenMac);
        AddEcho(extensions, IssueTicket && Offered(TlsExtensionType.SessionTicket), TlsExtensionType.SessionTicket);
        AddEcho(extensions, !resumed && OcspResponse is not null && Offered(TlsExtensionType.StatusRequest), TlsExtensionType.StatusRequest);
        if (ApplicationProtocol is not null && Offered(TlsExtensionType.ApplicationLayerProtocolNegotiation))
        {
            extensions.Add(ApplicationLayerProtocolNegotiationExtension.Encode([ApplicationProtocol]));
        }

        return extensions;
    }

    private static void AddEcho(List<TlsExtension> extensions, bool echo, TlsExtensionType type)
    {
        if (echo)
        {
            extensions.Add(new TlsExtension(type, []));
        }
    }

    private void AddFullFlight(List<Tls12OutgoingMessage> flight)
    {
        if (credential is not null)
        {
            Add(flight, new Tls12CertificateMessage([credential.Certificate, .. IssuerCertificates]).Encode());
            if (OcspResponse is not null && Offered(TlsExtensionType.StatusRequest) && !OmitCertificateStatus)
            {
                Add(flight, new HandshakeMessage(HandshakeType.CertificateStatus, StatusRequestExtension.EncodeOcspResponse(OcspResponse).Data).Encode());
            }
        }

        if (Suite.KeyExchange != Tls12KeyExchange.Rsa)
        {
            Add(flight, ServerKeyExchange().Encode());
        }

        if (RequestClientCertificate)
        {
            IReadOnlyList<ushort>? schemes = Version == TlsProtocolVersion.Tls12 ? ClientCertificateSchemes : null;
            Add(flight, new Tls12CertificateRequest([1, 64], schemes, []).Encode());
        }

        Add(flight, new HandshakeMessage(HandshakeType.ServerHelloDone, []).Encode());
    }

    private Tls12ServerKeyExchange ServerKeyExchange()
    {
        Tls12ServerKeyExchangeParameters parameters = Suite.KeyExchange == Tls12KeyExchange.Ecdhe ? EcdheParameters() : DheParameters();
        if (credential is null)
        {
            return new Tls12ServerKeyExchange(parameters, null, null);
        }

        byte[] content = [.. hello.Random, .. serverRandom, .. parameters.Encode()];
        if (Version != TlsProtocolVersion.Tls12)
        {
            return new Tls12ServerKeyExchange(parameters, null, LegacySignature(credential, content));
        }

        ushort scheme = SignatureScheme ?? credential.Scheme;
        return new Tls12ServerKeyExchange(parameters, scheme, credential.Sign(TlsSignatureScheme.FindTls12Rule(scheme)!, content));
    }

    // An Ed25519 key has no TLS 1.0 or 1.1 signature, so it sends one byte the client must refuse.
    private static byte[] LegacySignature(TestServerCredential credential, byte[] content)
    {
        if (credential.RsaKey is { } rsa)
        {
            return SignMd5Sha1(rsa, content);
        }

        TlsSignatureRule? rule = credential.SignsWithDsa ? TlsSignatureScheme.FindLegacyRule(TlsSignatureScheme.DsaOid) : TlsSignatureScheme.LegacyRules[1];
        return credential.SignsWithDsa || credential.BrainpoolKey is not null || credential.SigningKey.CanSign(rule) ? credential.Sign(rule!, content) : [0];
    }

    private Tls12EcdheParameters EcdheParameters()
    {
        ecdheShare = SystemTlsRandomSource.Instance.CreateKeyShare(EcdheGroup);
        return new Tls12EcdheParameters(EcdheGroup, ecdheShare.PublicKey);
    }

    private Tls12DheParameters DheParameters()
    {
        dheKey = FiniteFieldDiffieHellman.Generate(DheGroup);
        byte[] publicValue = new byte[DheGroup.PrimeLength];
        dheKey.ComputePublicValue(publicValue);
        return new Tls12DheParameters(DheGroup.Prime.ToArray(), DheGroup.Generator.ToArray(), publicValue);
    }

    private byte[] ComputeMasterSecret(byte[] exchangeKeys)
    {
        byte[] preMasterSecret = Suite.KeyExchange switch
        {
            Tls12KeyExchange.Rsa => credential!.RsaKey!.Decrypt(exchangeKeys, RSAEncryptionPadding.Pkcs1),
            Tls12KeyExchange.Ecdhe => ecdheShare!.ComputeSharedSecret(exchangeKeys)!,
            _ => DheSecret(exchangeKeys),
        };
        return extendedMasterSecret
            ? Prf.ComputeExtendedMasterSecret(preMasterSecret, Prf.HashHandshake(transcript.ToArray()))
            : Prf.ComputeMasterSecret(preMasterSecret, hello.Random, serverRandom);
    }

    private byte[] DheSecret(byte[] clientPublicValue)
    {
        byte[] secret = new byte[DheGroup.PrimeLength];
        Assert.IsTrue(dheKey!.TryComputeSharedSecret(clientPublicValue, secret));
        return secret[Array.FindIndex(secret, value => value != 0)..];
    }

    private void ReceiveClientCertificate(byte[] message)
    {
        Assert.AreEqual(HandshakeType.Certificate, (HandshakeType)message[0]);
        transcript.AddRange(message);
        ClientCertificates.AddRange(Tls12CertificateMessage.Decode(Body(message)).Value.CertificateList);
    }

    private void ReceiveCertificateVerify(byte[] message)
    {
        Assert.AreEqual(HandshakeType.CertificateVerify, (HandshakeType)message[0]);
        Tls12CertificateVerify verify = Tls12CertificateVerify.Decode(Body(message), Version == TlsProtocolVersion.Tls12).Value;
        TlsCertificatePublicKey key = TlsCertificatePublicKey.Read(ClientCertificates[0])!;
        TlsSignatureRule? rule = verify.SignatureAlgorithm is { } scheme
            ? TlsSignatureScheme.FindTls12Rule(scheme)
            : TlsSignatureScheme.FindLegacyRule(key.AlgorithmOid);
        Assert.IsNull(key.VerifySignature(rule, transcript.ToArray(), verify.Signature));
        transcript.AddRange(message);
    }

    private void ReceiveChangeCipherSpecAndFinished(Queue<Tls12OutgoingMessage> messages)
    {
        Tls12OutgoingMessage changeCipherSpec = messages.Dequeue();
        Assert.AreEqual(TlsContentType.ChangeCipherSpec, changeCipherSpec.ContentType);
        CollectionAssert.AreEqual(Tls12OutgoingMessage.ChangeCipherSpec.Bytes, changeCipherSpec.Bytes);
        byte[] expected = Prf.ComputeClientVerifyData(MasterSecret, Prf.HashHandshake(transcript.ToArray()));
        byte[] finished = Take(messages, HandshakeType.Finished);
        CollectionAssert.AreEqual(expected, Body(finished));
        Assert.IsEmpty(messages);
    }

    private void AddTicketChangeCipherSpecAndFinished(List<Tls12OutgoingMessage> flight)
    {
        if (IssueTicket && Offered(TlsExtensionType.SessionTicket))
        {
            byte[] ticket = RandomNumberGenerator.GetBytes(16);
            Sessions.AddTicket(ticket, new Tls12TestSession(MasterSecret, extendedMasterSecret));
            Add(flight, new Tls12NewSessionTicket(7200, ticket).Encode());
        }

        flight.Add(Tls12OutgoingMessage.ChangeCipherSpec);
        Add(flight, new Finished(Prf.ComputeServerVerifyData(MasterSecret, Prf.HashHandshake(transcript.ToArray()))).Encode());
    }

    private void DeriveKeyBlock()
    {
        Tls12RecordProtectionParameters parameters = Suite.RecordProtectionFor(Version, encryptThenMac);
        RecordProtection = parameters;
        KeyBlock = Tls12KeyBlock.Partition(parameters, Prf.ComputeKeyBlock(MasterSecret, serverRandom, hello.Random, parameters.KeyBlockLength));
    }

    private void Add(List<Tls12OutgoingMessage> flight, byte[] message)
    {
        flight.Add(Handshake(message));
        transcript.AddRange(message);
    }
}
