using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Curl.Cryptography;
using State = Curl.Tls.Tls12ClientHandshakeState;
using Step = Curl.Tls.Tls12ServerFlightStep;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.2, 1.1 and 1.0 client handshake (RFC 5246 section 7, RFC 4346, RFC 2246) as a
/// message-level state machine with no I/O (ADR-0140): the caller passes in the server's
/// handshake bytes and ChangeCipherSpec records, and sends the messages each step returns,
/// switching its record write state to <see cref="KeyBlock" />'s client keys after the
/// ChangeCipherSpec it sends, and its read state to the server keys after
/// <see cref="ReceiveChangeCipherSpec" /> accepts the server's. Covers ECDHE (X25519 and
/// the NIST curves), DHE with server-chosen parameters, RSA and anonymous key exchange
/// over every <see cref="Tls12CipherSuite" />, the ServerKeyExchange signature, secure
/// renegotiation's empty <c>renegotiation_info</c> (RFC 5746), the extended master secret
/// (RFC 7627), encrypt-then-MAC (RFC 7366), ALPN, SNI, <c>status_request</c> with the
/// CertificateStatus handed to the verifier, resumption by session ID and by ticket (RFC
/// 5077), and an optional client certificate; the server's chain goes to
/// <see cref="IServerCertificateVerifier" />.
/// </summary>
public sealed class Tls12ClientHandshake
{
    private const int PreMasterSecretLength = 48;

    // Below 1024 bits OpenSSL refuses the DHE group ("dh key too small") with handshake_failure.
    private const int MinimumDhPrimeBits = 1024;

    // RFC 7627, RFC 7366, RFC 5077 and RFC 6066: the ServerHello echoes these with empty data.
    private static readonly TlsExtensionType[] EmptyEchoes =
    [
        TlsExtensionType.ExtendedMasterSecret,
        TlsExtensionType.EncryptThenMac,
        TlsExtensionType.SessionTicket,
        TlsExtensionType.StatusRequest,
    ];

    private static readonly string[] RsaKeyOids = [TlsSignatureScheme.RsaEncryptionOid];

    private static readonly string[] EcdsaKeyOids = [TlsSignatureScheme.EcPublicKeyOid, TlsSignatureScheme.Ed25519Oid];

    private readonly Tls12ClientSettings settings;
    private readonly ITlsRandomSource random;
    private readonly IServerCertificateVerifier verifier;
    private readonly List<byte> received = [];
    private readonly List<byte> transcript = [];
    private ClientHello? clientHello;
    private byte[] serverRandom = [];
    private byte[] serverSessionId = [];
    private State state = State.Start;
    private Step lastStep;
    private bool certificateAwaitsVerification;
    private bool ticketExpected;
    private bool statusExpected;
    private bool encryptThenMac;
    private TlsCertificatePublicKey? serverKey;
    private Tls12CertificateRequest? certificateRequest;
    private byte[] preMasterSecret = [];
    private byte[] clientExchangeKeys = [];
    private byte[] masterSecret = [];
    private Tls12NewSessionTicket? newTicket;
    private object? certificateRejection;
    private OcspStapleOutcome? certificateStatusRejection;

    /// <summary>Creates a handshake that has not started.</summary>
    /// <param name="settings">What to offer and present.</param>
    /// <param name="random">Where the hello random, session ID, key shares and pre-master secret come from.</param>
    /// <param name="verifier">Verifies the server's certificate chain.</param>
    /// <exception cref="ArgumentException">The settings cannot drive a handshake.</exception>
    public Tls12ClientHandshake(Tls12ClientSettings settings, ITlsRandomSource random, IServerCertificateVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(verifier);
        settings.Validate();
        this.settings = settings;
        this.random = random;
        this.verifier = verifier;
    }

    /// <summary>Gets a value indicating whether the handshake has completed.</summary>
    public bool IsComplete => state == State.Connected;

    /// <summary>Gets why the handshake failed, or <see langword="null" /> while it has not.</summary>
    public TlsHandshakeFailure? Failure { get; private set; }

    /// <summary>Gets the version the server chose, once its ServerHello has arrived.</summary>
    public TlsProtocolVersion? Version { get; private set; }

    /// <summary>Gets the cipher suite the server chose, once its ServerHello has arrived.</summary>
    public Tls12CipherSuite? CipherSuite { get; private set; }

    /// <summary>Gets a value indicating whether the server resumed the offered session (an abbreviated handshake).</summary>
    public bool IsResumed { get; private set; }

    /// <summary>Gets a value indicating whether the master secret is the extended master secret (RFC 7627).</summary>
    public bool ExtendedMasterSecret { get; private set; }

    /// <summary>Gets the ECDHE group of the key exchange, or <see langword="null" /> for any other key exchange.</summary>
    public ushort? NegotiatedGroup { get; private set; }

    /// <summary>Gets the ALPN protocol the server chose, or <see langword="null" /> when it chose none.</summary>
    public string? ApplicationProtocol { get; private set; }

    /// <summary>Gets the server's DER certificates, leaf first, once its Certificate has arrived.</summary>
    public IReadOnlyList<byte[]> ServerCertificates { get; private set; } = [];

    /// <summary>Gets the DER OCSP response the server stapled in its CertificateStatus, or <see langword="null" />.</summary>
    public byte[]? OcspResponse { get; private set; }

    /// <summary>
    /// Gets the outcome of the stapled OCSP response check, once the verifier has accepted
    /// the server's chain in a full handshake, when <see cref="Tls12ClientSettings.RequestOcspStatus" />
    /// asks for one; otherwise <see langword="null" />. A resumed session presents no
    /// certificate, so it is not checked.
    /// </summary>
    public OcspStapleOutcome? CertificateStatus { get; private set; }

    /// <summary>Gets a value indicating whether the server sent a CertificateRequest.</summary>
    public bool ClientCertificateRequested => certificateRequest is not null;

    /// <summary>Gets a value indicating whether the client answered a CertificateRequest with a certificate and CertificateVerify.</summary>
    public bool ClientCertificateSent { get; private set; }

    /// <summary>Gets the record protection the suite gives, once the keys are known.</summary>
    public Tls12RecordProtectionParameters? RecordProtection { get; private set; }

    /// <summary>
    /// Gets both sides' write keys, once known: before the client's ChangeCipherSpec is
    /// returned in a full handshake, and from the ServerHello in a resumed one.
    /// </summary>
    public Tls12KeyBlock? KeyBlock { get; private set; }

    /// <summary>Gets the session to resume later, once the handshake has completed.</summary>
    public Tls12Session? Session { get; private set; }

    private TlsPrf Prf => CipherSuite!.PrfFor(Version!.Value);

    /// <summary>Starts the handshake: builds the ClientHello.</summary>
    /// <returns>The ClientHello to send.</returns>
    /// <exception cref="InvalidOperationException">The handshake has already started.</exception>
    public Tls12HandshakeOutput Start()
    {
        if (state != State.Start)
        {
            throw new InvalidOperationException("The handshake has already started.");
        }

        byte[] clientRandom = new byte[ClientHello.RandomLength];
        random.Fill(clientRandom);
        clientHello = Tls12ClientHelloBuilder.Build(settings, clientRandom, SessionIdToOffer());
        List<Tls12OutgoingMessage> output = [];
        Send(clientHello.Encode(), output);
        state = State.WaitServerHello;
        return Build(output);
    }

    /// <summary>
    /// Takes the content of handshake records from the server: any number of whole or
    /// partial messages. A message split across calls is held until it is whole. After a
    /// failure every call returns the same failure and nothing else.
    /// </summary>
    /// <param name="bytes">The handshake bytes.</param>
    /// <returns>What to send, and whether the handshake completed or failed.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Start" /> has not been called.</exception>
    public Tls12HandshakeOutput ReceiveHandshake(ReadOnlySpan<byte> bytes)
    {
        RequireStarted();
        List<Tls12OutgoingMessage> output = [];
        if (state != State.Failed)
        {
            received.AddRange(bytes);
            while (state != State.Failed && TryReadMessage(out HandshakeMessage? message, out byte[] encoded))
            {
                ReceiveMessage(message!, encoded, output);
            }
        }

        return Build(output);
    }

    /// <summary>
    /// Takes the content of the server's ChangeCipherSpec record. It is accepted only where
    /// the server's Finished is due next and no handshake message is partly received; then
    /// the caller switches its read state to <see cref="KeyBlock" />'s server keys.
    /// </summary>
    /// <param name="content">The record content, the single byte 1.</param>
    /// <returns>Nothing to send, or the failure: <c>unexpected_message</c> out of place, <c>decode_error</c> for other content.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Start" /> has not been called.</exception>
    public Tls12HandshakeOutput ReceiveChangeCipherSpec(ReadOnlySpan<byte> content)
    {
        RequireStarted();
        if (state == State.Failed)
        {
            return Build([]);
        }

        if (state != State.WaitChangeCipherSpec || received.Count > 0)
        {
            Fail(TlsAlertDescription.UnexpectedMessage);
        }
        else if (!content.SequenceEqual(Tls12OutgoingMessage.ChangeCipherSpec.Bytes))
        {
            Fail(TlsAlertDescription.DecodeError);
        }
        else
        {
            state = State.WaitFinished;
        }

        return Build([]);
    }

    private static byte[]? FindExtension(IReadOnlyList<TlsExtension> extensions, TlsExtensionType type) =>
        extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private static TlsAlertDescription? CheckRenegotiationInfo(byte[]? data)
    {
        // RFC 5746 section 3.4: without the extension the server may be open to renegotiation
        // attacks, and OpenSSL, like this client, refuses it (no SSL_OP_LEGACY_SERVER_CONNECT).
        if (data is null)
        {
            return TlsAlertDescription.HandshakeFailure;
        }

        TlsDecodeResult<byte[]> decoded = RenegotiationInfoExtension.Decode(data);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        return decoded.Value.Length == 0 ? null : TlsAlertDescription.HandshakeFailure;
    }

    private void RequireStarted()
    {
        if (state == State.Start)
        {
            throw new InvalidOperationException("Start the handshake before passing it the server's records.");
        }
    }

    private byte[] SessionIdToOffer()
    {
        Tls12Session? session = settings.SessionToResume;
        if (session?.Ticket is null)
        {
            return session?.SessionId ?? [];
        }

        // RFC 5077 section 3.4: a random session ID tells the client whether the ticket was accepted.
        byte[] sessionId = new byte[32];
        random.Fill(sessionId);
        return sessionId;
    }

    private bool TryReadMessage(out HandshakeMessage? message, out byte[] encoded)
    {
        HandshakeMessageReadResult read = HandshakeMessageReader.Read(CollectionsMarshal.AsSpan(received));
        message = read.Message;
        encoded = [.. received.Take(read.BytesConsumed)];
        received.RemoveRange(0, read.BytesConsumed);
        if (read.Alert is { } alert)
        {
            Fail(alert);
        }

        return message is not null;
    }

    private void ReceiveMessage(HandshakeMessage message, byte[] encoded, List<Tls12OutgoingMessage> output)
    {
        // RFC 5246 section 7.4.1.1: a HelloRequest stays out of the transcript, and a client
        // that never renegotiates ignores it.
        if (message.Type == HandshakeType.HelloRequest)
        {
            if (message.Body.Length != 0)
            {
                Fail(TlsAlertDescription.DecodeError);
            }

            return;
        }

        // The Finished is checked against the transcript before it and appends itself.
        if (message.Type != HandshakeType.Finished)
        {
            transcript.AddRange(encoded);
        }

        if (Dispatch(message.Type, message.Body, encoded, output) is { } alert)
        {
            Fail(alert);
        }
    }

    private TlsAlertDescription? Dispatch(HandshakeType type, byte[] body, byte[] encoded, List<Tls12OutgoingMessage> output)
    {
        if (state == State.WaitServerFlight)
        {
            return ReceiveServerFlightMessage(type, body, output);
        }

        if (type != ExpectedType())
        {
            return TlsAlertDescription.UnexpectedMessage;
        }

        return type switch
        {
            HandshakeType.ServerHello => ReceiveServerHello(body),
            HandshakeType.NewSessionTicket => ReceiveNewSessionTicket(body),
            _ => ReceiveFinished(body, encoded, output),
        };
    }

    /// <summary>The one message the state takes outside the server's flight, or none while a ChangeCipherSpec is due or after completion.</summary>
    private HandshakeType? ExpectedType() => state switch
    {
        State.WaitServerHello => HandshakeType.ServerHello,
        State.WaitNewSessionTicket => HandshakeType.NewSessionTicket,
        State.WaitFinished => HandshakeType.Finished,
        _ => null,
    };

    private TlsAlertDescription? ReceiveServerHello(byte[] body)
    {
        TlsDecodeResult<ServerHello> decoded = ServerHello.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        ServerHello hello = decoded.Value;
        TlsAlertDescription? alert = CheckServerVersion(hello) ?? CheckServerChoices(hello) ?? ReadServerHelloExtensions(hello.Extensions);
        if (alert is not null)
        {
            return alert;
        }

        Version = (TlsProtocolVersion)hello.LegacyVersion;
        CipherSuite = Tls12CipherSuite.Find(hello.CipherSuite);
        serverRandom = hello.Random;
        serverSessionId = hello.LegacySessionIdEcho;
        IsResumed = ResumesOfferedSession();
        return IsResumed ? Resume(settings.SessionToResume!) : StartFullHandshake();
    }

    private bool ResumesOfferedSession() =>
        settings.SessionToResume is not null
        && clientHello!.LegacySessionId.Length > 0
        && serverSessionId.AsSpan().SequenceEqual(clientHello.LegacySessionId);

    private TlsAlertDescription? CheckServerVersion(ServerHello hello)
    {
        TlsProtocolVersion version = (TlsProtocolVersion)hello.LegacyVersion;
        if (version < settings.MinimumVersion || version > settings.MaximumVersion)
        {
            return TlsAlertDescription.ProtocolVersion;
        }

        // RFC 8446 section 4.1.3: a TLS 1.2 client refuses a TLS 1.1 or 1.0 ServerHello that carries the downgrade sentinel.
        bool downgraded = settings.MaximumVersion == TlsProtocolVersion.Tls12
            && version != TlsProtocolVersion.Tls12
            && hello.Random.AsSpan(24).SequenceEqual("DOWNGRD\0"u8);
        return downgraded ? TlsAlertDescription.IllegalParameter : null;
    }

    private TlsAlertDescription? CheckServerChoices(ServerHello hello)
    {
        Tls12CipherSuite? suite = Tls12CipherSuite.Find(hello.CipherSuite);
        bool valid = suite is not null
            && settings.CipherSuites.Contains(suite.Code)
            && (!suite.RequiresTls12 || hello.LegacyVersion == (ushort)TlsProtocolVersion.Tls12)
            && hello.LegacyCompressionMethod == 0;
        return valid ? null : TlsAlertDescription.IllegalParameter;
    }

    private TlsAlertDescription? ReadServerHelloExtensions(IReadOnlyList<TlsExtension> extensions)
    {
        if (!extensions.All(extension => clientHello!.Extensions.Any(offered => offered.Type == extension.Type)))
        {
            return TlsAlertDescription.UnsupportedExtension;
        }

        if (extensions.Any(extension => EmptyEchoes.Contains(extension.Type) && extension.Data.Length != 0))
        {
            return TlsAlertDescription.DecodeError;
        }

        TlsAlertDescription? alert = CheckRenegotiationInfo(FindExtension(extensions, TlsExtensionType.RenegotiationInfo))
            ?? ReadApplicationProtocol(FindExtension(extensions, TlsExtensionType.ApplicationLayerProtocolNegotiation));
        ExtendedMasterSecret = FindExtension(extensions, TlsExtensionType.ExtendedMasterSecret) is not null;
        encryptThenMac = FindExtension(extensions, TlsExtensionType.EncryptThenMac) is not null;
        ticketExpected = FindExtension(extensions, TlsExtensionType.SessionTicket) is not null;
        statusExpected = FindExtension(extensions, TlsExtensionType.StatusRequest) is not null;
        return alert;
    }

    private TlsAlertDescription? ReadApplicationProtocol(byte[]? data)
    {
        if (data is null)
        {
            return null;
        }

        TlsDecodeResult<IReadOnlyList<string>> decoded = ApplicationLayerProtocolNegotiationExtension.Decode(data);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        if (decoded.Value.Count != 1 || !settings.ApplicationProtocols.Contains(decoded.Value[0]))
        {
            return TlsAlertDescription.IllegalParameter;
        }

        ApplicationProtocol = decoded.Value[0];
        return null;
    }

    private TlsAlertDescription? Resume(Tls12Session session)
    {
        if (Version != session.Version || CipherSuite!.Code != session.CipherSuite)
        {
            return TlsAlertDescription.IllegalParameter;
        }

        // RFC 7627 section 5.3: the resumption must agree with the session on the extended master secret.
        if (ExtendedMasterSecret != session.ExtendedMasterSecret)
        {
            return TlsAlertDescription.HandshakeFailure;
        }

        masterSecret = session.MasterSecret;
        InstallKeys();
        AwaitServerFinish();
        return null;
    }

    private TlsAlertDescription? StartFullHandshake()
    {
        lastStep = Step.ServerHello;
        state = State.WaitServerFlight;
        return null;
    }

    private TlsAlertDescription? ReceiveServerFlightMessage(HandshakeType type, byte[] body, List<Tls12OutgoingMessage> output)
    {
        Step step = StepOf(type);
        if (!CanFollow(step))
        {
            return TlsAlertDescription.UnexpectedMessage;
        }

        lastStep = step;

        // The chain goes to the verifier once any CertificateStatus has had its chance to arrive.
        TlsAlertDescription? alert = step > Step.CertificateStatus && certificateAwaitsVerification ? VerifyServerCertificates() : null;
        return alert ?? step switch
        {
            Step.Certificate => ReceiveCertificate(body),
            Step.CertificateStatus => ReceiveCertificateStatus(body),
            Step.ServerKeyExchange => ReceiveServerKeyExchange(body),
            Step.CertificateRequest => ReceiveCertificateRequest(body),
            _ => ReceiveServerHelloDone(body, output),
        };
    }

    private static Step StepOf(HandshakeType type) => type switch
    {
        HandshakeType.Certificate => Step.Certificate,
        HandshakeType.CertificateStatus => Step.CertificateStatus,
        HandshakeType.ServerKeyExchange => Step.ServerKeyExchange,
        HandshakeType.CertificateRequest => Step.CertificateRequest,
        HandshakeType.ServerHelloDone => Step.ServerHelloDone,
        _ => Step.ServerHello,
    };

    private bool CanFollow(Step step)
    {
        if (step <= lastStep || !IsPermitted(step))
        {
            return false;
        }

        for (Step skipped = lastStep + 1; skipped < step; skipped++)
        {
            if (IsRequired(skipped))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPermitted(Step step) => step switch
    {
        Step.Certificate or Step.CertificateRequest => CipherSuite!.Authentication != Tls12Authentication.Anonymous,
        Step.CertificateStatus => statusExpected,
        Step.ServerKeyExchange => CipherSuite!.KeyExchange != Tls12KeyExchange.Rsa,
        _ => true,
    };

    private bool IsRequired(Step step) => step switch
    {
        Step.Certificate => CipherSuite!.Authentication != Tls12Authentication.Anonymous,
        Step.ServerKeyExchange => CipherSuite!.KeyExchange != Tls12KeyExchange.Rsa,
        _ => false,
    };

    private TlsAlertDescription? ReceiveCertificate(byte[] body)
    {
        TlsDecodeResult<Tls12CertificateMessage> decoded = Tls12CertificateMessage.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        if (decoded.Value.CertificateList.Count == 0)
        {
            return TlsAlertDescription.DecodeError;
        }

        ServerCertificates = decoded.Value.CertificateList;
        serverKey = TlsCertificatePublicKey.Read(ServerCertificates[0]);
        if (serverKey is null)
        {
            return TlsAlertDescription.BadCertificate;
        }

        string[] keyOids = CipherSuite!.Authentication == Tls12Authentication.Rsa ? RsaKeyOids : EcdsaKeyOids;
        certificateAwaitsVerification = true;
        return keyOids.Contains(serverKey.AlgorithmOid) ? null : TlsAlertDescription.HandshakeFailure;
    }

    private TlsAlertDescription? VerifyServerCertificates()
    {
        certificateAwaitsVerification = false;
        ServerCertificateVerdict verdict = verifier.Verify(new ServerCertificateChain(ServerCertificates, settings.ServerName, OcspResponse));
        if (verdict.IsAccepted)
        {
            return CheckCertificateStatus();
        }

        certificateRejection = verdict.Rejection;
        return verdict.Alert;
    }

    /// <summary>With <c>--cert-status</c>, checks the CertificateStatus response, or its absence, once the verifier has accepted the chain.</summary>
    private TlsAlertDescription? CheckCertificateStatus()
    {
        if (!settings.RequestOcspStatus)
        {
            return null;
        }

        CertificateStatus = OcspStapleVerifier.Verify(OcspResponse, ServerCertificates, settings.TimeProvider.GetUtcNow());
        if (CertificateStatus.IsGood)
        {
            return null;
        }

        certificateStatusRejection = CertificateStatus;
        return TlsAlertDescription.BadCertificateStatusResponse;
    }

    private TlsAlertDescription? ReceiveCertificateStatus(byte[] body)
    {
        TlsDecodeResult<byte[]> decoded = StatusRequestExtension.DecodeOcspResponse(body);
        OcspResponse = decoded.Succeeded ? decoded.Value : null;
        return decoded.Alert;
    }

    private TlsAlertDescription? ReceiveServerKeyExchange(byte[] body)
    {
        bool signed = CipherSuite!.Authentication != Tls12Authentication.Anonymous;
        TlsDecodeResult<Tls12ServerKeyExchange> decoded =
            Tls12ServerKeyExchange.Decode(body, CipherSuite.KeyExchange, signed, Version == TlsProtocolVersion.Tls12);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        Tls12ServerKeyExchange message = decoded.Value;
        TlsAlertDescription? alert = signed ? CheckServerKeyExchangeSignature(message) : null;
        return alert ?? message.Parameters switch
        {
            Tls12EcdheParameters ecdhe => AgreeEcdhe(ecdhe),
            _ => AgreeDhe((Tls12DheParameters)message.Parameters),
        };
    }

    private TlsAlertDescription? CheckServerKeyExchangeSignature(Tls12ServerKeyExchange message)
    {
        TlsSignatureRule? rule = Version == TlsProtocolVersion.Tls12
            ? FindOfferedRule(message.SignatureAlgorithm!.Value)
            : TlsSignatureScheme.FindLegacyRule(serverKey!.AlgorithmOid);
        return serverKey!.VerifySignature(rule, message.BuildSignedContent(clientHello!.Random, serverRandom), message.Signature!);
    }

    private TlsSignatureRule? FindOfferedRule(ushort scheme) =>
        settings.SignatureAlgorithms.Contains(scheme) ? TlsSignatureScheme.FindTls12Rule(scheme) : null;

    private TlsAlertDescription? AgreeEcdhe(Tls12EcdheParameters parameters)
    {
        if (!settings.SupportedGroups.Contains(parameters.NamedGroup))
        {
            return TlsAlertDescription.IllegalParameter;
        }

        using Tls13KeyShare share = random.CreateKeyShare(parameters.NamedGroup);
        byte[]? sharedSecret = share.ComputeSharedSecret(parameters.PublicKey);
        if (sharedSecret is null)
        {
            return TlsAlertDescription.IllegalParameter;
        }

        NegotiatedGroup = parameters.NamedGroup;
        preMasterSecret = sharedSecret;
        clientExchangeKeys = share.PublicKey;
        return null;
    }

    private TlsAlertDescription? AgreeDhe(Tls12DheParameters parameters)
    {
        if (!FiniteFieldDiffieHellmanGroup.TryCreate(parameters.Prime, parameters.Generator, out FiniteFieldDiffieHellmanGroup? group))
        {
            return TlsAlertDescription.IllegalParameter;
        }

        if (new BigInteger(group.Prime, isUnsigned: true, isBigEndian: true).GetBitLength() < MinimumDhPrimeBits)
        {
            return TlsAlertDescription.HandshakeFailure;
        }

        byte[] exponent = new byte[Math.Min(FiniteFieldDiffieHellman.GeneratedExponentLength, group.PrimeLength - 1)];
        random.Fill(exponent);
        exponent[0] |= 0x80;
        using FiniteFieldDiffieHellman key = new(group, exponent);
        CryptographicOperations.ZeroMemory(exponent);
        byte[] sharedSecret = new byte[group.PrimeLength];
        // A composite "prime" (p = q^2 with Ys = q) can make Z zero; a zero Z is refused like an invalid Ys.
        int firstNonZero = key.TryComputeSharedSecret(parameters.PublicValue, sharedSecret) ? Array.FindIndex(sharedSecret, value => value != 0) : -1;
        if (firstNonZero < 0)
        {
            return TlsAlertDescription.IllegalParameter;
        }

        clientExchangeKeys = new byte[group.PrimeLength];
        key.ComputePublicValue(clientExchangeKeys);

        // RFC 5246 section 8.1.2: the pre-master secret is Z with its leading zero bytes stripped.
        preMasterSecret = sharedSecret[firstNonZero..];
        return null;
    }

    private TlsAlertDescription? ReceiveCertificateRequest(byte[] body)
    {
        TlsDecodeResult<Tls12CertificateRequest> decoded = Tls12CertificateRequest.Decode(body, Version == TlsProtocolVersion.Tls12);
        certificateRequest = decoded.Succeeded ? decoded.Value : null;
        return decoded.Alert;
    }

    private TlsAlertDescription? ReceiveServerHelloDone(byte[] body, List<Tls12OutgoingMessage> output)
    {
        if (body.Length != 0)
        {
            return TlsAlertDescription.DecodeError;
        }

        if (!PrepareRsaKeyExchange())
        {
            return TlsAlertDescription.BadCertificate;
        }

        ClientSignature? signature = certificateRequest is null ? null : SendClientCertificate(output);
        Send(new Tls12ClientKeyExchange(CipherSuite!.KeyExchange, clientExchangeKeys).Encode(), output);
        masterSecret = ComputeMasterSecret();
        SendCertificateVerify(signature, output);
        InstallKeys();
        SendChangeCipherSpecAndFinished(output);
        AwaitServerFinish();
        return null;
    }

    /// <summary>After the client's Finished, or a resumed ServerHello: a promised NewSessionTicket, else the server's ChangeCipherSpec.</summary>
    private void AwaitServerFinish() => state = ticketExpected ? State.WaitNewSessionTicket : State.WaitChangeCipherSpec;

    /// <summary>For RSA key exchange, encrypts the pre-master secret; ECDHE and DHE agreed theirs at the ServerKeyExchange.</summary>
    private bool PrepareRsaKeyExchange() => CipherSuite!.KeyExchange != Tls12KeyExchange.Rsa || EncryptPreMasterSecret();

    private bool EncryptPreMasterSecret()
    {
        // RFC 5246 section 7.4.7.1: the offered version, then 46 random bytes.
        byte[] secret = new byte[PreMasterSecretLength];
        secret[0] = (byte)((ushort)settings.MaximumVersion >> 8);
        secret[1] = (byte)settings.MaximumVersion;
        random.Fill(secret.AsSpan(2));
        byte[]? encrypted = serverKey!.EncryptPkcs1(secret);
        preMasterSecret = secret;
        clientExchangeKeys = encrypted ?? [];
        return encrypted is not null;
    }

    private ClientSignature? SendClientCertificate(List<Tls12OutgoingMessage> output)
    {
        TlsClientCertificate? certificate = settings.ClientCertificate;
        ClientSignature? signature = certificate is null ? null : ChooseClientSignature(certificate.SigningKey);

        // RFC 5246 section 7.4.6: with no suitable certificate the client sends an empty one and no CertificateVerify.
        Send(new Tls12CertificateMessage(signature is null ? [] : certificate!.CertificateChain).Encode(), output);
        ClientCertificateSent = signature is not null;
        return signature;
    }

    private ClientSignature? ChooseClientSignature(TlsSigningKey key)
    {
        if (Version != TlsProtocolVersion.Tls12)
        {
            TlsSignatureRule? legacyRule = TlsSignatureScheme.LegacyRules.FirstOrDefault(key.CanSign);
            return legacyRule is null ? null : new ClientSignature(legacyRule, null);
        }

        ushort scheme = certificateRequest!.SignatureAlgorithms!.FirstOrDefault(offered => key.CanSign(FindOfferedRule(offered)));
        return scheme == 0 ? null : new ClientSignature(TlsSignatureScheme.FindTls12Rule(scheme)!, scheme);
    }

    private void SendCertificateVerify(ClientSignature? signature, List<Tls12OutgoingMessage> output)
    {
        if (signature is null)
        {
            return;
        }

        // RFC 5246 section 7.4.8: the signature covers every handshake message so far.
        byte[] content = [.. transcript];
        Send(new Tls12CertificateVerify(signature.Scheme, settings.ClientCertificate!.SigningKey.SignByRule(signature.Rule, content)).Encode(), output);
    }

    private byte[] ComputeMasterSecret()
    {
        TlsPrf prf = Prf;
        byte[] secret = ExtendedMasterSecret
            ? prf.ComputeExtendedMasterSecret(preMasterSecret, prf.HashHandshake(CollectionsMarshal.AsSpan(transcript)))
            : prf.ComputeMasterSecret(preMasterSecret, clientHello!.Random, serverRandom);
        CryptographicOperations.ZeroMemory(preMasterSecret);
        return secret;
    }

    private void InstallKeys()
    {
        RecordProtection = CipherSuite!.RecordProtectionFor(Version!.Value, encryptThenMac);
        byte[] keyBlock = Prf.ComputeKeyBlock(masterSecret, serverRandom, clientHello!.Random, RecordProtection.KeyBlockLength);
        KeyBlock = Tls12KeyBlock.Partition(RecordProtection, keyBlock);
    }

    private void SendChangeCipherSpecAndFinished(List<Tls12OutgoingMessage> output)
    {
        output.Add(Tls12OutgoingMessage.ChangeCipherSpec);
        byte[] verifyData = Prf.ComputeClientVerifyData(masterSecret, Prf.HashHandshake(CollectionsMarshal.AsSpan(transcript)));
        Send(new Finished(verifyData).Encode(), output);
    }

    private TlsAlertDescription? ReceiveNewSessionTicket(byte[] body)
    {
        TlsDecodeResult<Tls12NewSessionTicket> decoded = Tls12NewSessionTicket.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        newTicket = decoded.Value;
        state = State.WaitChangeCipherSpec;
        return null;
    }

    private TlsAlertDescription? ReceiveFinished(byte[] body, byte[] encoded, List<Tls12OutgoingMessage> output)
    {
        byte[] expected = Prf.ComputeServerVerifyData(masterSecret, Prf.HashHandshake(CollectionsMarshal.AsSpan(transcript)));
        if (!CryptographicOperations.FixedTimeEquals(expected, body))
        {
            return TlsAlertDescription.DecryptError;
        }

        transcript.AddRange(encoded);
        if (IsResumed)
        {
            SendChangeCipherSpecAndFinished(output);
        }

        Session = BuildSession();
        state = State.Connected;
        return null;
    }

    private Tls12Session BuildSession()
    {
        // A resumed session keeps its ticket unless the server issued a new one.
        Tls12NewSessionTicket? ticket = newTicket ?? ResumedTicket();
        return new Tls12Session(Version!.Value, CipherSuite!.Code, serverSessionId, ticket?.Ticket, ticket?.LifetimeHint ?? 0, masterSecret, ExtendedMasterSecret);
    }

    private Tls12NewSessionTicket? ResumedTicket()
    {
        Tls12Session? resumed = IsResumed ? settings.SessionToResume : null;
        return resumed?.Ticket is { } ticket ? new Tls12NewSessionTicket(resumed.TicketLifetimeHint, ticket) : null;
    }

    private void Send(byte[] message, List<Tls12OutgoingMessage> output)
    {
        transcript.AddRange(message);
        output.Add(new Tls12OutgoingMessage(TlsContentType.Handshake, message));
    }

    private Tls12HandshakeOutput Build(List<Tls12OutgoingMessage> output) => new(output, IsComplete, Failure);

    private void Fail(TlsAlertDescription alert)
    {
        state = State.Failed;
        Failure = new TlsHandshakeFailure(alert, certificateRejection) { CertificateStatusRejection = certificateStatusRejection };
    }

    /// <summary>The rule the client's CertificateVerify signs by, and the scheme it names in TLS 1.2.</summary>
    private sealed record ClientSignature(TlsSignatureRule Rule, ushort? Scheme);
}
