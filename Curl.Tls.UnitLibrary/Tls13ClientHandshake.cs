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
/// ECDSA and Ed25519, the server Finished check, and an optional client certificate, asked
/// for during the handshake or, once <c>post_handshake_auth</c> was offered, after it (RFC
/// 8446 section 4.6.2); the server's chain, decompressed first when it arrives as a
/// CompressedCertificate (RFC 8879), goes to <see cref="IServerCertificateVerifier" />.
/// </summary>
public sealed class Tls13ClientHandshake : IDisposable
{
    private static readonly TlsExtensionType[] RetryRequestExtensions =
        [TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions, TlsExtensionType.Cookie];

    private static readonly TlsExtensionType[] ServerHelloExtensions = [TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions];

    private static readonly TlsExtensionType[] ResumedServerHelloExtensions =
        [TlsExtensionType.KeyShare, TlsExtensionType.SupportedVersions, TlsExtensionType.PreSharedKey];

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
    private readonly List<TlsSessionRecord> sessions = [];
    private List<Tls13KeyShare> shares = [];
    private TlsSessionRecord? offeredSession;
    private Tls13PskOffer? pskOffer;
    private byte[] sessionEarlySecret = [];
    private Tls13ClientHelloBuilder? helloBuilder;
    private ClientHello? clientHello;
    private byte[] clientHelloBytes = [];
    private State state = State.Start;
    private bool retried;
    private TranscriptHash? transcript;
    private byte[] clientHandshakeTrafficSecret = [];
    private byte[] serverHandshakeTrafficSecret = [];
    private byte[] masterSecret = [];
    private byte[] clientApplicationTrafficSecret = [];
    private TlsCertificatePublicKey? serverKey;
    private IReadOnlyList<ushort>? requestedSchemes;
    private object? certificateRejection;
    private OcspStapleOutcome? certificateStatusRejection;

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

    /// <summary>
    /// Gets the outcome of the stapled OCSP response check, once the verifier has accepted
    /// the server's Certificate, when <see cref="Tls13ClientSettings.RequestOcspStatus" />
    /// asks for one; otherwise <see langword="null" />.
    /// </summary>
    public OcspStapleOutcome? CertificateStatus { get; private set; }

    /// <summary>Gets a value indicating whether the server sent a CertificateRequest, during the handshake or after it.</summary>
    public bool ClientCertificateRequested => requestedSchemes is not null;

    /// <summary>Gets a value indicating whether the client answered a CertificateRequest, during the handshake or after it, with a certificate and CertificateVerify.</summary>
    public bool ClientCertificateSent { get; private set; }

    /// <summary>Gets <c>exporter_master_secret</c>, once the server's Finished has been checked.</summary>
    public byte[]? ExporterMasterSecret { get; private set; }

    /// <summary>Gets <c>resumption_master_secret</c>, once the client's Finished has been sent.</summary>
    public byte[]? ResumptionMasterSecret { get; private set; }

    /// <summary>Gets the NewSessionTicket messages received after the handshake, in order.</summary>
    public IReadOnlyList<NewSessionTicket> ReceivedTickets => tickets;

    /// <summary>
    /// Gets a session record for each NewSessionTicket received, in order: the ticket, its
    /// resumption PSK (RFC 8446 section 4.6.1), its lifetime, age add and early data limit,
    /// the suite, group, host name, ALPN protocol and server certificate, and when it arrived
    /// by <see cref="Tls13ClientSettings.TimeProvider" />. Any of them resumes through
    /// <see cref="Tls13ClientSettings.ResumptionSession" />.
    /// </summary>
    public IReadOnlyList<TlsSessionRecord> ReceivedSessions => sessions;

    /// <summary>Gets a value indicating whether the server accepted the offered ticket, once its ServerHello has arrived: no Certificate or CertificateVerify follows.</summary>
    public bool IsResumed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the first ClientHello offered 0-RTT early data. From
    /// <see cref="Start" /> on, the caller may send up to <see cref="MaxEarlyDataSize" /> bytes
    /// at <see cref="TlsEncryptionLevel.EarlyData" /> under <see cref="EarlyDataCipherSuite" />.
    /// </summary>
    public bool EarlyDataOffered { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the server accepted the early data, once its
    /// EncryptedExtensions has arrived. When it did not, the server discarded it, and the
    /// caller sends it again as application data after the handshake.
    /// </summary>
    public bool EarlyDataAccepted { get; private set; }

    /// <summary>Gets the most early data the offered ticket allows, in bytes, or zero when no early data was offered.</summary>
    public uint MaxEarlyDataSize => EarlyDataOffered ? offeredSession!.MaxEarlyDataSize : 0;

    /// <summary>Gets the suite that protects early data (the resumed session's), or <see langword="null" /> when no early data was offered.</summary>
    public Tls13CipherSuite? EarlyDataCipherSuite => EarlyDataOffered ? Tls13CipherSuite.Find(offeredSession!.CipherSuite) : null;

    /// <summary>Gets the last ClientHello <see cref="Start" /> or a HelloRetryRequest sent, or <see langword="null" /> before the start.</summary>
    internal ClientHello? SentClientHello => clientHello;

    private TranscriptHash Transcript => transcript!;

    private Tls13KeySchedule Schedule => CipherSuite!.KeySchedule;

    private Tls13KeySchedule SessionSchedule => Tls13CipherSuite.Find(offeredSession!.CipherSuite)!.KeySchedule;

    private TlsEncryptionLevel ExpectedLevel => state switch
    {
        State.WaitServerHello => TlsEncryptionLevel.Initial,
        State.Connected => TlsEncryptionLevel.Application,
        _ => TlsEncryptionLevel.Handshake,
    };

    /// <summary>
    /// Starts the handshake: builds the ClientHello to send at the Initial level, offering
    /// <see cref="Tls13ClientSettings.ResumptionSession" /> when it can be resumed, and with
    /// early data offered installs <c>client_early_traffic_secret</c> for writing at the
    /// <see cref="TlsEncryptionLevel.EarlyData" /> level.
    /// </summary>
    /// <returns>The ClientHello to send, and the early traffic secret when early data is offered.</returns>
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
        OfferSession(settings.TimeProvider.GetUtcNow());
        Tls13HandshakeOutputBuilder output = new();
        SendClientHello(null, output);
        if (EarlyDataOffered)
        {
            byte[] clientHelloHash = CryptographicOperations.HashData(SessionSchedule.HashAlgorithm, clientHelloBytes);
            output.Install(TlsEncryptionLevel.EarlyData, TlsTrafficDirection.Write, SessionSchedule.DeriveClientEarlyTrafficSecret(sessionEarlySecret, clientHelloHash));
        }

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

    /// <summary>
    /// Moves <c>client_application_traffic_secret_N</c> on to N+1 (RFC 8446 section 7.2) as
    /// the client sends a KeyUpdate, and returns it; a later post-handshake Finished is keyed
    /// from the secret in force (section 4.4).
    /// </summary>
    /// <returns>The next client application traffic secret.</returns>
    internal byte[] AdvanceClientApplicationTrafficSecret() =>
        clientApplicationTrafficSecret = Schedule.DeriveNextApplicationTrafficSecret(clientApplicationTrafficSecret);

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
        State.WaitCertificateOrRequest or State.WaitCertificate => ExpectedCertificateType(arrived),
        State.WaitCertificateVerify => HandshakeType.CertificateVerify,
        State.WaitFinished => HandshakeType.Finished,
        _ => ExpectedPostHandshakeType(arrived),
    };

    /// <summary>RFC 8446 section 4.6.2: a CertificateRequest may follow the handshake only once <c>post_handshake_auth</c> was offered.</summary>
    private HandshakeType ExpectedPostHandshakeType(HandshakeType arrived) =>
        arrived == HandshakeType.CertificateRequest && WasOffered(TlsExtensionType.PostHandshakeAuth)
            ? HandshakeType.CertificateRequest
            : HandshakeType.NewSessionTicket;

    /// <summary>RFC 8879 section 4: a CompressedCertificate may replace the Certificate once <c>compress_certificate</c> was offered.</summary>
    private HandshakeType ExpectedCertificateType(HandshakeType arrived) =>
        arrived == HandshakeType.CompressedCertificate && settings.CertificateCompressionAlgorithms.Count > 0
            ? HandshakeType.CompressedCertificate
            : HandshakeType.Certificate;

    private TlsAlertDescription? Dispatch(HandshakeType type, byte[] body, byte[] encoded, Tls13HandshakeOutputBuilder output) => type switch
    {
        HandshakeType.ServerHello => ReceiveServerHello(body, encoded, output),
        HandshakeType.EncryptedExtensions => ReceiveEncryptedExtensions(body, encoded),
        HandshakeType.CertificateRequest => ReceiveCertificateRequest(body, encoded, output),
        HandshakeType.CompressedCertificate => ReceiveCompressedCertificate(body, encoded),
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

        // RFC 8446 sections 4.1.2 and 4.2.10: the second hello never offers early data, and
        // keeps the ticket only when the suite the server chose shares its hash.
        pskOffer = pskOffer is not null && CipherSuite!.KeySchedule.HashAlgorithm == SessionSchedule.HashAlgorithm
            ? pskOffer with { EarlyData = false }
            : null;
        SendClientHello(cookie, output);
    }

    private TlsAlertDescription? ReceiveKeyShare(ServerHello hello, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        TlsAlertDescription? alert = CheckExtensionTypes(hello.Extensions, pskOffer is null ? ServerHelloExtensions : ResumedServerHelloExtensions)
            ?? ReadSelectedIdentity(FindExtension(hello.Extensions, TlsExtensionType.PreSharedKey), hello.CipherSuite);
        return alert ?? ReceiveServerShare(hello, encoded, output);
    }

    /// <summary>Computes the shared secret from the ServerHello's <c>key_share</c> and the client's share of its group, and enters the handshake keys.</summary>
    private TlsAlertDescription? ReceiveServerShare(ServerHello hello, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
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

    /// <summary>
    /// RFC 8446 section 4.2.11: a ServerHello's <c>pre_shared_key</c> selects the one identity
    /// offered, index 0, with a suite of the ticket's hash; anything else is <c>illegal_parameter</c>.
    /// </summary>
    private TlsAlertDescription? ReadSelectedIdentity(byte[]? data, ushort cipherSuite)
    {
        if (data is null)
        {
            return null;
        }

        TlsDecodeResult<ushort> selected = PreSharedKeyExtension.DecodeSelected(data);
        if (!selected.Succeeded)
        {
            return selected.Alert;
        }

        IsResumed = selected.Value == 0 && Tls13CipherSuite.Find(cipherSuite)!.KeySchedule.HashAlgorithm == SessionSchedule.HashAlgorithm;
        return IsResumed ? null : TlsAlertDescription.IllegalParameter;
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
        byte[] earlySecret = IsResumed ? sessionEarlySecret : Schedule.ComputeEarlySecret(null);
        byte[] handshakeSecret = Schedule.ComputeHandshakeSecret(earlySecret, sharedSecret);
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
        TlsAlertDescription? alert = ReadEncryptedExtensions(extensions);
        if (alert is not null)
        {
            return alert;
        }

        ServerQuicTransportParameters = FindExtension(extensions, TlsExtensionType.QuicTransportParameters);
        Transcript.Append(encoded);

        // RFC 8446 section 2.2: a resumed handshake authenticates with the PSK, so the server's Finished comes next.
        state = IsResumed ? State.WaitFinished : State.WaitCertificateOrRequest;
        return null;
    }

    /// <summary>Checks each EncryptedExtensions extension may be there, then reads the ALPN protocol and the early data indication.</summary>
    private TlsAlertDescription? ReadEncryptedExtensions(IReadOnlyList<TlsExtension> extensions) =>
        extensions.Select(extension => CheckEncryptedExtensionType(extension.Type)).FirstOrDefault(found => found is not null)
            ?? ReadApplicationProtocol(FindExtension(extensions, TlsExtensionType.ApplicationLayerProtocolNegotiation))
            ?? ReadEarlyDataIndication(FindExtension(extensions, TlsExtensionType.EarlyData));

    /// <summary>
    /// RFC 8446 section 4.2.10: an empty <c>early_data</c> accepts the early data, and only
    /// for the ticket offered, resumed with its own suite and ALPN protocol; otherwise it is
    /// <c>illegal_parameter</c>. Without one the server rejected (and skipped) it.
    /// </summary>
    private TlsAlertDescription? ReadEarlyDataIndication(byte[]? data)
    {
        if (data is null)
        {
            return null;
        }

        bool acceptable = IsResumed
            && CipherSuite!.Code == offeredSession!.CipherSuite
            && ApplicationProtocol == offeredSession.ApplicationProtocol;
        TlsAlertDescription? alert = EarlyDataExtension.DecodeIndication(data) ?? (acceptable ? null : TlsAlertDescription.IllegalParameter);
        EarlyDataAccepted = alert is null;
        return alert;
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

    private TlsAlertDescription? ReceiveCertificateRequest(byte[] body, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        TlsDecodeResult<CertificateRequest> decoded = CertificateRequest.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        // RFC 8446 section 4.3.2: the context is empty during the handshake and names a request after it.
        bool afterHandshake = state == State.Connected;
        byte[] context = decoded.Value.CertificateRequestContext;
        if ((context.Length != 0) != afterHandshake)
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
        if (afterHandshake)
        {
            AnswerPostHandshakeRequest(context, schemes.Value, encoded, output);
            return null;
        }

        Transcript.Append(encoded);
        state = State.WaitCertificate;
        return null;
    }

    /// <summary>
    /// Answers a post-handshake CertificateRequest (RFC 8446 section 4.6.2) at the
    /// Application level: Certificate echoing its context, CertificateVerify when a
    /// certificate fits, and Finished keyed from the client application traffic secret in
    /// force, over the handshake's transcript plus this request and answer (section 4.4).
    /// The handshake's own transcript is left as it was for the next request.
    /// </summary>
    private void AnswerPostHandshakeRequest(byte[] context, IReadOnlyList<ushort> schemes, byte[] encoded, Tls13HandshakeOutputBuilder output)
    {
        using TranscriptHash requestTranscript = Transcript.Clone();
        requestTranscript.Append(encoded);
        SendClientCertificate(context, schemes, requestTranscript, TlsEncryptionLevel.Application, output);
        Finished finished = new(Schedule.ComputeFinishedVerifyData(clientApplicationTrafficSecret, requestTranscript.GetCurrentHash()));
        SendHandshakeMessage(finished.Encode(), requestTranscript, TlsEncryptionLevel.Application, output);
    }

    /// <summary>
    /// RFC 8879 section 4: an algorithm not offered, a wrong <c>uncompressed_length</c> or
    /// data that does not decompress is <c>bad_certificate</c>; the decompressed body is then
    /// read as the Certificate, and the CompressedCertificate as sent enters the transcript.
    /// </summary>
    private TlsAlertDescription? ReceiveCompressedCertificate(byte[] body, byte[] encoded)
    {
        TlsDecodeResult<CompressedCertificate> decoded = CompressedCertificate.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        byte[]? certificateBody = decoded.Value.Decompress(settings.CertificateCompressionAlgorithms);
        return certificateBody is null ? TlsAlertDescription.BadCertificate : ReceiveCertificate(certificateBody, encoded);
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
        alert = VerifyServerCertificates(ocspResponse);
        if (alert is not null)
        {
            return alert;
        }

        Transcript.Append(encoded);
        state = State.WaitCertificateVerify;
        return null;
    }

    /// <summary>Hands the chain to the verifier, then with <c>--cert-status</c> checks the response stapled to the leaf.</summary>
    private TlsAlertDescription? VerifyServerCertificates(byte[]? ocspResponse)
    {
        ServerCertificateVerdict verdict = verifier.Verify(new ServerCertificateChain(ServerCertificates, settings.ServerName, ocspResponse));
        if (!verdict.IsAccepted)
        {
            certificateRejection = verdict.Rejection;
            return verdict.Alert;
        }

        return CheckCertificateStatus(ocspResponse);
    }

    private TlsAlertDescription? CheckCertificateStatus(byte[]? ocspResponse)
    {
        if (!settings.RequestOcspStatus)
        {
            return null;
        }

        CertificateStatus = OcspStapleVerifier.Verify(ocspResponse, ServerCertificates, settings.TimeProvider.GetUtcNow());
        if (CertificateStatus.IsGood)
        {
            return null;
        }

        certificateStatusRejection = CertificateStatus;
        return TlsAlertDescription.BadCertificateStatusResponse;
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
        clientApplicationTrafficSecret = Schedule.DeriveClientApplicationTrafficSecret(masterSecret, serverFinishedHash);
        ExporterMasterSecret = Schedule.DeriveExporterMasterSecret(masterSecret, serverFinishedHash);
        if (EarlyDataAccepted && settings.SendEndOfEarlyData)
        {
            // RFC 8446 section 4.5: accepted early data ends, under the early keys, before the client's flight.
            SendHandshakeMessage(new HandshakeMessage(HandshakeType.EndOfEarlyData, []).Encode(), Transcript, TlsEncryptionLevel.EarlyData, output);
        }

        if (requestedSchemes is not null)
        {
            SendClientCertificate([], requestedSchemes, Transcript, TlsEncryptionLevel.Handshake, output);
        }

        Finished finished = new(Schedule.ComputeFinishedVerifyData(clientHandshakeTrafficSecret, Transcript.GetCurrentHash()));
        SendHandshakeMessage(finished.Encode(), output);
        output.Install(TlsEncryptionLevel.Application, TlsTrafficDirection.Write, clientApplicationTrafficSecret);
        ResumptionMasterSecret = Schedule.DeriveResumptionMasterSecret(masterSecret, Transcript.GetCurrentHash());
        state = State.Connected;
        return null;
    }

    private void SendClientCertificate(
        byte[] context,
        IReadOnlyList<ushort> schemes,
        TranscriptHash to,
        TlsEncryptionLevel level,
        Tls13HandshakeOutputBuilder output)
    {
        TlsClientCertificate? certificate = settings.ClientCertificate;
        ushort scheme = certificate is null ? (ushort)0 : schemes.FirstOrDefault(certificate.SigningKey.CanSign);
        if (scheme == 0)
        {
            // RFC 8446 section 4.4.2: no suitable certificate is an empty Certificate and no CertificateVerify.
            SendHandshakeMessage(new CertificateMessage(context, []).Encode(), to, level, output);
            return;
        }

        CertificateEntry[] entries = [.. certificate!.CertificateChain.Select(der => new CertificateEntry(der, []))];
        SendHandshakeMessage(new CertificateMessage(context, entries).Encode(), to, level, output);
        byte[] content = TlsSignatureScheme.BuildCertificateVerifyContent(false, to.GetCurrentHash());
        SendHandshakeMessage(new CertificateVerify(scheme, certificate.SigningKey.Sign(scheme, content)).Encode(), to, level, output);
        ClientCertificateSent = true;
    }

    private TlsAlertDescription? ReceiveNewSessionTicket(byte[] body)
    {
        TlsDecodeResult<NewSessionTicket> decoded = NewSessionTicket.Decode(body);
        if (!decoded.Succeeded)
        {
            return decoded.Alert;
        }

        byte[]? earlyData = FindExtension(decoded.Value.Extensions, TlsExtensionType.EarlyData);
        TlsDecodeResult<uint> maxEarlyDataSize = earlyData is null ? TlsDecodeResult<uint>.Success(0) : EarlyDataExtension.DecodeMaxEarlyDataSize(earlyData);
        if (!maxEarlyDataSize.Succeeded)
        {
            return maxEarlyDataSize.Alert;
        }

        tickets.Add(decoded.Value);
        sessions.Add(RecordSession(decoded.Value, maxEarlyDataSize.Value));
        return null;
    }

    /// <summary>The session a NewSessionTicket stands for: its PSK is HKDF-Expand-Label(resumption_master_secret, "resumption", ticket_nonce) (RFC 8446 section 4.6.1).</summary>
    private TlsSessionRecord RecordSession(NewSessionTicket ticket, uint maxEarlyDataSize) =>
        new(
            Tls13ClientHelloBuilder.Tls13Version,
            CipherSuite!.Code,
            SHA256.HashData(ticket.Ticket),
            Schedule.DeriveResumptionPreSharedKey(ResumptionMasterSecret!, ticket.TicketNonce),
            ticket.Ticket,
            ticket.TicketLifetime,
            ticket.TicketAgeAdd,
            maxEarlyDataSize,
            settings.TimeProvider.GetUtcNow())
        {
            ServerName = settings.ServerName,
            ApplicationProtocol = ApplicationProtocol,
            Group = NegotiatedGroup!.Value,
            PeerCertificate = IsResumed ? offeredSession!.PeerCertificate : ServerCertificates[0],
        };

    /// <summary>
    /// Chooses to offer <see cref="Tls13ClientSettings.ResumptionSession" /> when it can be
    /// resumed at <paramref name="now" /> with an offered suite's hash for the same host, and
    /// early data with it when asked for and the ticket, its suite (which the server must
    /// choose) and its ALPN protocol allow it (RFC 8446 section 4.2.10).
    /// </summary>
    private void OfferSession(DateTimeOffset now)
    {
        TlsSessionRecord? session = settings.ResumptionSession;
        if (session is null || !CanOffer(session, now))
        {
            return;
        }

        offeredSession = session;
        sessionEarlySecret = SessionSchedule.ComputeEarlySecret(session.PreSharedKey);
        EarlyDataOffered = CanSendEarlyData(session);
        PskIdentity identity = new(session.Ticket, session.ObfuscatedTicketAgeAt(now));
        pskOffer = new Tls13PskOffer(identity, SessionSchedule.HashLength, EarlyDataOffered);
    }

    private bool CanOffer(TlsSessionRecord session, DateTimeOffset now) =>
        Tls13CipherSuite.Find(session.CipherSuite) is { } sessionSuite
            && session.CanResumeAt(now)
            && string.Equals(session.ServerName, settings.ServerName, StringComparison.OrdinalIgnoreCase)
            && settings.CipherSuites.Any(code => Tls13CipherSuite.Find(code)!.KeySchedule.HashAlgorithm == sessionSuite.KeySchedule.HashAlgorithm);

    /// <summary>Early data goes with the ticket when asked for, when the ticket allows some, and when the settings offer its suite and ALPN protocol.</summary>
    private bool CanSendEarlyData(TlsSessionRecord session) =>
        settings.OfferEarlyData
            && session.MaxEarlyDataSize > 0
            && settings.CipherSuites.Contains(session.CipherSuite)
            && (session.ApplicationProtocol is null || settings.ApplicationProtocols.Contains(session.ApplicationProtocol));

    /// <summary>
    /// Replaces the zero binder the builder left with the real one (RFC 8446 section
    /// 4.2.11.2): the Finished HMAC under the resumption binder key over the transcript so far
    /// and the hello up to its binders list.
    /// </summary>
    private ClientHello BindPsk(ClientHello hello)
    {
        byte[] encoded = hello.Encode();
        ReadOnlySpan<byte> truncated = encoded.AsSpan(0, encoded.Length - (3 + pskOffer!.BinderLength));
        using TranscriptHash prefix = transcript?.Clone() ?? SessionSchedule.CreateTranscriptHash();
        prefix.Append(truncated);
        byte[] binderKey = SessionSchedule.DeriveResumptionBinderKey(sessionEarlySecret);
        byte[] binder = SessionSchedule.ComputePskBinder(binderKey, prefix.GetCurrentHash());
        TlsExtension offered = PreSharedKeyExtension.EncodeOffered(new OfferedPsks([pskOffer.Identity], [binder]));
        return hello with { Extensions = [.. hello.Extensions.SkipLast(1), offered] };
    }

    private void SendClientHello(byte[]? cookie, Tls13HandshakeOutputBuilder output)
    {
        ClientHello hello = helloBuilder!.Build([.. shares.Select(share => share.Entry)], cookie, pskOffer);
        clientHello = pskOffer is null ? hello : BindPsk(hello);
        clientHelloBytes = clientHello.Encode();
        transcript?.Append(clientHelloBytes);
        output.Send(TlsEncryptionLevel.Initial, clientHelloBytes);
    }

    private void SendHandshakeMessage(byte[] message, Tls13HandshakeOutputBuilder output) =>
        SendHandshakeMessage(message, Transcript, TlsEncryptionLevel.Handshake, output);

    private static void SendHandshakeMessage(byte[] message, TranscriptHash to, TlsEncryptionLevel level, Tls13HandshakeOutputBuilder output)
    {
        to.Append(message);
        output.Send(level, message);
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
        Failure = new TlsHandshakeFailure(alert, certificateRejection) { CertificateStatusRejection = certificateStatusRejection };
    }
}
