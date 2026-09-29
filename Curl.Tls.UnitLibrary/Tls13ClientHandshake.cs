using System.Runtime.InteropServices;
using System.Security.Cryptography;
using State = Curl.Tls.Tls13ClientHandshakeState;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.3 client handshake (RFC 8446) as a message-level state machine with no I/O
/// (ADR-0140): handshake bytes go in at an encryption level, and each step returns the
/// bytes to send at each level, the traffic secrets that now apply, and completion or a
/// typed failure. QUIC drives it with CRYPTO frames (RFC 9001 section 4.1); over TCP the
/// record layer drives it. Covers the full handshake with a HelloRetryRequest, key shares
/// on X25519, the NIST curves and the finite-field groups, CertificateVerify with RSA-PSS,
/// ECDSA and Ed25519, the server Finished check, and an optional client certificate; the
/// server's chain goes to <see cref="IServerCertificateVerifier" />.
/// </summary>
public sealed class Tls13ClientHandshake : IDisposable
{
    private static readonly TlsExtensionType[] RetryRequestExtensions =
        [TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions, TlsExtensionType.Cookie];

    private static readonly TlsExtensionType[] ServerHelloExtensions = [TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions];

    private static readonly TlsExtensionType[] ForbiddenInEncryptedExtensions =
    [
        TlsExtensionType.StatusRequest,
        TlsExtensionType.SignatureAlgorithms,
        TlsExtensionType.Padding,
        TlsExtensionType.PreSharedKey,
        TlsExtensionType.SupportedVersions,
        TlsExtensionType.Cookie,
        TlsExtensionType.PskKeyExchangeModes,
        TlsExtensionType.CertificateAuthorities,
        TlsExtensionType.PostHandshakeAuth,
        TlsExtensionType.SignatureAlgorithmsCert,
        TlsExtensionType.KeyShare,
    ];

    private readonly Tls13ClientSettings settings;
    private readonly ITlsRandomSource random;
    private readonly IServerCertificateVerifier verifier;
    private readonly List<byte> received = [];
    private readonly List<NewSessionTicket> tickets = [];
    private List<Tls13KeyShare> shares = [];
    private Tls13ClientHelloBuilder? helloBuilder;
    private ClientHello? clientHello;
    private byte[] clientHelloBytes = [];
    private State state = State.Start;
    private bool retried;
    private TranscriptHash? transcript;
    private byte[] clientHandshakeTrafficSecret = [];
    private byte[] serverHandshakeTrafficSecret = [];
    private byte[] masterSecret = [];
    private TlsCertificatePublicKey? serverKey;
    private IReadOnlyList<ushort>? requestedSchemes;
    private object? certificateRejection;

    /// <summary>Creates a handshake that has not started.</summary>
    /// <param name="settings">What to offer and present.</param>
    /// <param name="random">Where the hello random, session ID and key shares come from.</param>
    /// <param name="verifier">Verifies the server's certificate chain.</param>
    /// <exception cref="ArgumentException">The settings cannot drive a handshake.</exception>
    public Tls13ClientHandshake(Tls13ClientSettings settings, ITlsRandomSource random, IServerCertificateVerifier verifier)
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

    /// <summary>Gets the cipher suite the server chose, once its ServerHello or HelloRetryRequest has arrived.</summary>
    public Tls13CipherSuite? CipherSuite { get; private set; }

    /// <summary>Gets the group of the key exchange, once the ServerHello has arrived.</summary>
    public ushort? NegotiatedGroup { get; private set; }

    /// <summary>Gets the ALPN protocol the server chose, or <see langword="null" /> when it chose none.</summary>
    public string? ApplicationProtocol { get; private set; }

    /// <summary>
    /// Gets the data of the server's <c>quic_transport_parameters</c> extension (RFC 9001
    /// section 8.2) once its EncryptedExtensions has arrived, or <see langword="null" /> when
    /// it sent none. QUIC (<c>Curl.Quic</c>) decodes and checks them.
    /// </summary>
    public byte[]? ServerQuicTransportParameters { get; private set; }

    /// <summary>Gets the server's DER certificates, leaf first, once its Certificate has arrived.</summary>
    public IReadOnlyList<byte[]> ServerCertificates { get; private set; } = [];

    /// <summary>Gets a value indicating whether the server sent a CertificateRequest.</summary>
    public bool ClientCertificateRequested => requestedSchemes is not null;

    /// <summary>Gets a value indicating whether the client answered a CertificateRequest with a certificate and CertificateVerify.</summary>
    public bool ClientCertificateSent { get; private set; }

    /// <summary>Gets <c>exporter_master_secret</c>, once the server's Finished has been checked.</summary>
    public byte[]? ExporterMasterSecret { get; private set; }

    /// <summary>Gets <c>resumption_master_secret</c>, once the client's Finished has been sent.</summary>
    public byte[]? ResumptionMasterSecret { get; private set; }

    /// <summary>Gets the NewSessionTicket messages received after the handshake, in order.</summary>
    public IReadOnlyList<NewSessionTicket> ReceivedTickets => tickets;

    private TranscriptHash Transcript => transcript!;

    private Tls13KeySchedule Schedule => CipherSuite!.KeySchedule;

    private TlsEncryptionLevel ExpectedLevel => state switch
    {
        State.WaitServerHello => TlsEncryptionLevel.Initial,
        State.Connected => TlsEncryptionLevel.Application,
        _ => TlsEncryptionLevel.Handshake,
    };

    /// <summary>Starts the handshake: builds the ClientHello to send at the Initial level.</summary>
    /// <returns>The ClientHello to send.</returns>
    /// <exception cref="InvalidOperationException">The handshake has already started.</exception>
    public Tls13HandshakeOutput Start()
    {
        if (state != State.Start)
        {
            throw new InvalidOperationException("The handshake has already started.");
        }

        byte[] clientRandom = new byte[ClientHello.RandomLength];
        random.Fill(clientRandom);
        byte[] legacySessionId = new byte[settings.SendLegacySessionId ? 32 : 0];
        random.Fill(legacySessionId);
        helloBuilder = new Tls13ClientHelloBuilder(settings, clientRandom, legacySessionId);
        shares = [.. settings.KeyShareGroups.Select(random.CreateKeyShare)];
        Tls13HandshakeOutputBuilder output = new();
        SendClientHello(null, output);
        state = State.WaitServerHello;
        return output.Build(false, null);
    }

    /// <summary>
    /// Takes handshake bytes the server sent at <paramref name="level" />: any number of
    /// whole or partial messages. A message split across calls is held until it is whole.
    /// After a failure every call returns the same failure and nothing else.
    /// </summary>
    /// <param name="level">The encryption level the bytes arrived at.</param>
    /// <param name="bytes">The handshake bytes.</param>
    /// <returns>What to send and install, and whether the handshake completed or failed.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Start" /> has not been called.</exception>
    public Tls13HandshakeOutput Receive(TlsEncryptionLevel level, ReadOnlySpan<byte> bytes)
    {
        if (state == State.Start)
        {
            throw new InvalidOperationException("Start the handshake before passing it the server's bytes.");
        }

        Tls13HandshakeOutputBuilder output = new();
        if (state != State.Failed)
        {
            ReceiveAtLevel(level, bytes, output);
        }

        return output.Build(IsComplete, Failure);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeShares();
        transcript?.Dispose();
    }

    private static byte[]? FindExtension(IReadOnlyList<TlsExtension> extensions, TlsExtensionType type) =>
        extensions.FirstOrDefault(extension => extension.Type == type)?.Data;

    private static TlsAlertDescription? CheckExtensionTypes(IReadOnlyList<TlsExtension> extensions, TlsExtensionType[] allowed) =>
        extensions.All(extension => allowed.Contains(extension.Type)) ? null : TlsAlertDescription.UnsupportedExtension;

    private static bool HasDowngradeSentinel(byte[] serverRandom) =>
        serverRandom.AsSpan(24, 7).SequenceEqual("DOWNGRD"u8) && serverRandom[31] <= 1;

    private static TlsAlertDescription? DecodeRetryCookie(byte[]? data, out byte[]? cookie)
    {
        cookie = null;
        if (data is null)
        {
            return null;
        }

        TlsDecodeResult<byte[]> decoded = CookieExtension.Decode(data);
        cookie = decoded.Succeeded ? decoded.Value : null;
        return decoded.Alert;
    }

    private void ReceiveAtLevel(TlsEncryptionLevel level, ReadOnlySpan<byte> bytes, Tls13HandshakeOutputBuilder output)
    {
        if (level != ExpectedLevel)
        {
            Fail(TlsAlertDescription.UnexpectedMessage);
            return;
        }

        received.AddRange(bytes);
        while (state != State.Failed && TryReadMessage(out HandshakeMessage? message, out byte[] encoded))
        {
            ReceiveMessage(level, message!, encoded, output);
        }
    }

    private void ReceiveMessage(TlsEncryptionLevel level, HandshakeMessage message, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        TlsAlertDescription? alert = message.Type == ExpectedType(message.Type)
            ? Dispatch(message.Type, message.Body, encoded, output)
            : TlsAlertDescription.UnexpectedMessage;

        // RFC 8446 section 5.1: a handshake message may not span a change of keys.
        bool bytesSpanKeyChange = received.Count > 0 && ExpectedLevel != level;
        if (alert is null && bytesSpanKeyChange)
        {
            alert = TlsAlertDescription.UnexpectedMessage;
        }

        if (alert is { } description)
        {
            Fail(description);
        }
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

    private HandshakeType ExpectedType(HandshakeType arrived) => state switch
    {
        State.WaitServerHello => HandshakeType.ServerHello,
        State.WaitEncryptedExtensions => HandshakeType.EncryptedExtensions,
        State.WaitCertificateOrRequest when arrived == HandshakeType.CertificateRequest => HandshakeType.CertificateRequest,
        State.WaitCertificateOrRequest or State.WaitCertificate => HandshakeType.Certificate,
        State.WaitCertificateVerify => HandshakeType.CertificateVerify,
        State.WaitFinished => HandshakeType.Finished,
        _ => HandshakeType.NewSessionTicket,
    };

    private TlsAlertDescription? Dispatch(HandshakeType type, byte[] body, byte[] encoded, Tls13HandshakeOutputBuilder output) => type switch
    {
        HandshakeType.ServerHello => ReceiveServerHello(body, encoded, output),
        HandshakeType.EncryptedExtensions => ReceiveEncryptedExtensions(body, encoded),
        HandshakeType.CertificateRequest => ReceiveCertificateRequest(body, encoded),
        _ => DispatchAfterCertificateRequest(type, body, encoded, output),
    };

    private TlsAlertDescription? DispatchAfterCertificateRequest(HandshakeType type, byte[] body, byte[] encoded, Tls13HandshakeOutputBuilder output) => type switch
    {
        HandshakeType.Certificate => ReceiveCertificate(body, encoded),
        HandshakeType.CertificateVerify => ReceiveCertificateVerify(body, encoded),
        HandshakeType.Finished => ReceiveFinished(body, encoded, output),
        _ => ReceiveNewSessionTicket(body),
    };

    private TlsAlertDescription? ReceiveServerHello(byte[] body, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        TlsDecodeResult<ServerHello> decoded = ServerHello.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        ServerHello hello = decoded.Value;
        TlsAlertDescription? alert = CheckSelectedVersion(hello) ?? CheckEchoedFields(hello);
        if (alert is not null)
        {
            return alert;
        }

        return hello.IsHelloRetryRequest ? ReceiveHelloRetryRequest(hello, encoded, output) : ReceiveKeyShare(hello, encoded, output);
    }

    private static TlsAlertDescription? CheckSelectedVersion(ServerHello hello)
    {
        byte[]? data = FindExtension(hello.Extensions, TlsExtensionType.SupportedVersions);
        if (data is null)
        {
            // A TLS 1.2 or older ServerHello; with the RFC 8446 section 4.1.3 sentinel it is a downgrade.
            return HasDowngradeSentinel(hello.Random) ? TlsAlertDescription.IllegalParameter : TlsAlertDescription.ProtocolVersion;
        }

        TlsDecodeResult<ushort> selected = SupportedVersionsExtension.DecodeSelected(data);
        if (!selected.Succeeded)
        {
            return selected.Alert;
        }

        return selected.Value == Tls13ClientHelloBuilder.Tls13Version ? null : TlsAlertDescription.IllegalParameter;
    }

    private TlsAlertDescription? CheckEchoedFields(ServerHello hello)
    {
        bool valid = hello.LegacySessionIdEcho.AsSpan().SequenceEqual(clientHello!.LegacySessionId)
            && hello.LegacyCompressionMethod == 0
            && settings.CipherSuites.Contains(hello.CipherSuite)
            && (CipherSuite is null || CipherSuite.Code == hello.CipherSuite);
        return valid ? null : TlsAlertDescription.IllegalParameter;
    }

    private TlsAlertDescription? ReceiveHelloRetryRequest(ServerHello hello, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        TlsAlertDescription? alert = retried ? TlsAlertDescription.UnexpectedMessage : CheckExtensionTypes(hello.Extensions, RetryRequestExtensions);
        if (alert is not null)
        {
            return alert;
        }

        alert = DecodeRetryChanges(hello.Extensions, out ushort? group, out byte[]? cookie);
        if (alert is not null)
        {
            return alert;
        }

        Retry(hello.CipherSuite, encoded, group, cookie, output);
        return null;
    }

    private TlsAlertDescription? DecodeRetryChanges(IReadOnlyList<TlsExtension> extensions, out ushort? group, out byte[]? cookie)
    {
        byte[]? keyShareData = FindExtension(extensions, TlsExtensionType.KeyShare);
        byte[]? cookieData = FindExtension(extensions, TlsExtensionType.Cookie);
        TlsAlertDescription? groupAlert = DecodeRetryGroup(keyShareData, out group);
        TlsAlertDescription? cookieAlert = DecodeRetryCookie(cookieData, out cookie);
        bool changesNothing = keyShareData is null && cookieData is null;

        // RFC 8446 section 4.1.4: a HelloRetryRequest that would not change the ClientHello is illegal.
        return changesNothing ? TlsAlertDescription.IllegalParameter : groupAlert ?? cookieAlert;
    }

    private TlsAlertDescription? DecodeRetryGroup(byte[]? data, out ushort? group)
    {
        group = null;
        if (data is null)
        {
            return null;
        }

        TlsDecodeResult<ushort> decoded = KeyShareExtension.DecodeSelectedGroup(data);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        group = decoded.Value;
        bool offeredWithoutShare = settings.SupportedGroups.Contains(decoded.Value) && shares.TrueForAll(share => share.Group != decoded.Value);
        return offeredWithoutShare ? null : TlsAlertDescription.IllegalParameter;
    }

    private void Retry(ushort cipherSuite, byte[] encoded, ushort? group, byte[]? cookie, Tls13HandshakeOutputBuilder output)
    {
        retried = true;
        CipherSuite = Tls13CipherSuite.Find(cipherSuite);
        StartTranscript();
        Transcript.ReplaceWithMessageHash();
        Transcript.Append(encoded);
        if (group is { } retryGroup)
        {
            DisposeShares();
            shares = [random.CreateKeyShare(retryGroup)];
        }

        SendClientHello(cookie, output);
    }

    private TlsAlertDescription? ReceiveKeyShare(ServerHello hello, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        TlsAlertDescription? alert = CheckExtensionTypes(hello.Extensions, ServerHelloExtensions);
        if (alert is not null)
        {
            return alert;
        }

        byte[]? data = FindExtension(hello.Extensions, TlsExtensionType.KeyShare);
        if (data is null)
        {
            return TlsAlertDescription.MissingExtension;
        }

        TlsDecodeResult<KeyShareEntry> serverShare = KeyShareExtension.DecodeServerShare(data);
        if (!serverShare.Succeeded)
        {
            return serverShare.Alert;
        }

        Tls13KeyShare? share = shares.Find(candidate => candidate.Group == serverShare.Value.Group);
        byte[]? sharedSecret = share?.ComputeSharedSecret(serverShare.Value.KeyExchange);
        if (sharedSecret is null)
        {
            return TlsAlertDescription.IllegalParameter;
        }

        EnterHandshakeKeys(hello.CipherSuite, encoded, serverShare.Value.Group, sharedSecret, output);
        return null;
    }

    private void EnterHandshakeKeys(ushort cipherSuite, byte[] encoded, ushort group, byte[] sharedSecret, Tls13HandshakeOutputBuilder output)
    {
        CipherSuite ??= Tls13CipherSuite.Find(cipherSuite);
        if (transcript is null)
        {
            StartTranscript();
        }

        Transcript.Append(encoded);
        NegotiatedGroup = group;
        DisposeShares();
        byte[] handshakeSecret = Schedule.ComputeHandshakeSecret(Schedule.ComputeEarlySecret(null), sharedSecret);
        byte[] serverHelloHash = Transcript.GetCurrentHash();
        clientHandshakeTrafficSecret = Schedule.DeriveClientHandshakeTrafficSecret(handshakeSecret, serverHelloHash);
        serverHandshakeTrafficSecret = Schedule.DeriveServerHandshakeTrafficSecret(handshakeSecret, serverHelloHash);
        masterSecret = Schedule.ComputeMasterSecret(handshakeSecret);
        output.Install(TlsEncryptionLevel.Handshake, TlsTrafficDirection.Read, serverHandshakeTrafficSecret);
        output.Install(TlsEncryptionLevel.Handshake, TlsTrafficDirection.Write, clientHandshakeTrafficSecret);
        state = State.WaitEncryptedExtensions;
    }

    private TlsAlertDescription? ReceiveEncryptedExtensions(byte[] body, byte[] encoded)
    {
        TlsDecodeResult<EncryptedExtensions> decoded = EncryptedExtensions.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        IReadOnlyList<TlsExtension> extensions = decoded.Value.Extensions;
        TlsAlertDescription? alert = extensions.Select(extension => CheckEncryptedExtensionType(extension.Type)).FirstOrDefault(found => found is not null)
            ?? ReadApplicationProtocol(FindExtension(extensions, TlsExtensionType.ApplicationLayerProtocolNegotiation));
        if (alert is not null)
        {
            return alert;
        }

        ServerQuicTransportParameters = FindExtension(extensions, TlsExtensionType.QuicTransportParameters);
        Transcript.Append(encoded);
        state = State.WaitCertificateOrRequest;
        return null;
    }

    private TlsAlertDescription? CheckEncryptedExtensionType(TlsExtensionType type)
    {
        if (ForbiddenInEncryptedExtensions.Contains(type))
        {
            return TlsAlertDescription.IllegalParameter;
        }

        return type == TlsExtensionType.SupportedGroups || WasOffered(type) ? null : TlsAlertDescription.UnsupportedExtension;
    }

    private bool WasOffered(TlsExtensionType type) => clientHello!.Extensions.Any(extension => extension.Type == type);

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

    private TlsAlertDescription? ReceiveCertificateRequest(byte[] body, byte[] encoded)
    {
        TlsDecodeResult<CertificateRequest> decoded = CertificateRequest.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        if (decoded.Value.CertificateRequestContext.Length != 0)
        {
            return TlsAlertDescription.IllegalParameter;
        }

        byte[]? data = FindExtension(decoded.Value.Extensions, TlsExtensionType.SignatureAlgorithms);
        if (data is null)
        {
            return TlsAlertDescription.MissingExtension;
        }

        TlsDecodeResult<IReadOnlyList<ushort>> schemes = SignatureAlgorithmsExtension.Decode(data);
        if (!schemes.Succeeded)
        {
            return schemes.Alert;
        }

        requestedSchemes = schemes.Value;
        Transcript.Append(encoded);
        state = State.WaitCertificate;
        return null;
    }

    private TlsAlertDescription? ReceiveCertificate(byte[] body, byte[] encoded)
    {
        TlsDecodeResult<CertificateMessage> decoded = CertificateMessage.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        CertificateMessage message = decoded.Value;
        TlsAlertDescription? alert = CheckCertificateShape(message);
        if (alert is not null)
        {
            return alert;
        }

        alert = ReadCertificateEntryExtensions(message.CertificateList, out byte[]? ocspResponse);
        if (alert is not null)
        {
            return alert;
        }

        ServerCertificates = [.. message.CertificateList.Select(entry => entry.CertificateData)];
        ServerCertificateVerdict verdict = verifier.Verify(new ServerCertificateChain(ServerCertificates, settings.ServerName, ocspResponse));
        if (!verdict.IsAccepted)
        {
            certificateRejection = verdict.Rejection;
            return verdict.Alert;
        }

        Transcript.Append(encoded);
        state = State.WaitCertificateVerify;
        return null;
    }

    private TlsAlertDescription? CheckCertificateShape(CertificateMessage message)
    {
        if (message.CertificateRequestContext.Length != 0)
        {
            return TlsAlertDescription.IllegalParameter;
        }

        if (message.CertificateList.Count == 0)
        {
            // RFC 8446 section 4.4.2.4: an empty server Certificate is a decode_error.
            return TlsAlertDescription.DecodeError;
        }

        serverKey = TlsCertificatePublicKey.Read(message.CertificateList[0].CertificateData);
        return serverKey is null ? TlsAlertDescription.BadCertificate : null;
    }

    private TlsAlertDescription? ReadCertificateEntryExtensions(IReadOnlyList<CertificateEntry> entries, out byte[]? ocspResponse)
    {
        ocspResponse = null;
        if (!entries.All(entry => entry.Extensions.All(extension => WasOffered(extension.Type))))
        {
            return TlsAlertDescription.UnsupportedExtension;
        }

        byte[]? data = FindExtension(entries[0].Extensions, TlsExtensionType.StatusRequest);
        if (data is null)
        {
            return null;
        }

        TlsDecodeResult<byte[]> decoded = StatusRequestExtension.DecodeOcspResponse(data);
        ocspResponse = decoded.Succeeded ? decoded.Value : null;
        return decoded.Alert;
    }

    private TlsAlertDescription? ReceiveCertificateVerify(byte[] body, byte[] encoded)
    {
        TlsDecodeResult<CertificateVerify> decoded = CertificateVerify.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        if (!settings.SignatureAlgorithms.Contains(decoded.Value.Algorithm))
        {
            return TlsAlertDescription.IllegalParameter;
        }

        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(true, Transcript.GetCurrentHash());
        TlsAlertDescription? alert = serverKey!.VerifySignature(decoded.Value.Algorithm, content, decoded.Value.Signature);
        if (alert is not null)
        {
            return alert;
        }

        Transcript.Append(encoded);
        state = State.WaitFinished;
        return null;
    }

    private TlsAlertDescription? ReceiveFinished(byte[] body, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        byte[] expected = Schedule.ComputeFinishedVerifyData(serverHandshakeTrafficSecret, Transcript.GetCurrentHash());
        if (!CryptographicOperations.FixedTimeEquals(expected, body))
        {
            return TlsAlertDescription.DecryptError;
        }

        Transcript.Append(encoded);
        byte[] serverFinishedHash = Transcript.GetCurrentHash();
        output.Install(TlsEncryptionLevel.Application, TlsTrafficDirection.Read, Schedule.DeriveServerApplicationTrafficSecret(masterSecret, serverFinishedHash));
        byte[] clientApplicationTrafficSecret = Schedule.DeriveClientApplicationTrafficSecret(masterSecret, serverFinishedHash);
        ExporterMasterSecret = Schedule.DeriveExporterMasterSecret(masterSecret, serverFinishedHash);
        if (requestedSchemes is not null)
        {
            SendClientCertificate(requestedSchemes, output);
        }

        Finished finished = new(Schedule.ComputeFinishedVerifyData(clientHandshakeTrafficSecret, Transcript.GetCurrentHash()));
        SendHandshakeMessage(finished.Encode(), output);
        output.Install(TlsEncryptionLevel.Application, TlsTrafficDirection.Write, clientApplicationTrafficSecret);
        ResumptionMasterSecret = Schedule.DeriveResumptionMasterSecret(masterSecret, Transcript.GetCurrentHash());
        state = State.Connected;
        return null;
    }

    private void SendClientCertificate(IReadOnlyList<ushort> schemes, Tls13HandshakeOutputBuilder output)
    {
        TlsClientCertificate? certificate = settings.ClientCertificate;
        ushort scheme = certificate is null ? (ushort)0 : schemes.FirstOrDefault(certificate.SigningKey.CanSign);
        if (scheme == 0)
        {
            // RFC 8446 section 4.4.2: no suitable certificate is an empty Certificate and no CertificateVerify.
            SendHandshakeMessage(new CertificateMessage([], []).Encode(), output);
            return;
        }

        CertificateEntry[] entries = [.. certificate!.CertificateChain.Select(der => new CertificateEntry(der, []))];
        SendHandshakeMessage(new CertificateMessage([], entries).Encode(), output);
        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(false, Transcript.GetCurrentHash());
        SendHandshakeMessage(new CertificateVerify(scheme, certificate.SigningKey.Sign(scheme, content)).Encode(), output);
        ClientCertificateSent = true;
    }

    private TlsAlertDescription? ReceiveNewSessionTicket(byte[] body)
    {
        TlsDecodeResult<NewSessionTicket> decoded = NewSessionTicket.Decode(body);
        if (decoded.Succeeded)
        {
            tickets.Add(decoded.Value);
        }

        return decoded.Alert;
    }

    private void SendClientHello(byte[]? cookie, Tls13HandshakeOutputBuilder output)
    {
        clientHello = helloBuilder!.Build([.. shares.Select(share => share.Entry)], cookie);
        clientHelloBytes = clientHello.Encode();
        transcript?.Append(clientHelloBytes);
        output.Send(TlsEncryptionLevel.Initial, clientHelloBytes);
    }

    private void SendHandshakeMessage(byte[] message, Tls13HandshakeOutputBuilder output)
    {
        Transcript.Append(message);
        output.Send(TlsEncryptionLevel.Handshake, message);
    }

    private void StartTranscript()
    {
        transcript = Schedule.CreateTranscriptHash();
        transcript.Append(clientHelloBytes);
    }

    private void DisposeShares()
    {
        foreach (Tls13KeyShare share in shares)
        {
            share.Dispose();
        }

        shares = [];
    }

    private void Fail(TlsAlertDescription alert)
    {
        state = State.Failed;
        Failure = new TlsHandshakeFailure(alert, certificateRejection);
    }
}
