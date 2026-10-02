using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The second <see cref="ITlsProvider" /> (ADR-0140): runs the client handshake with the
/// hand-built TLS client in <c>Curl.Tls.UnitLibrary</c> over the plaintext connection, for
/// the option sets <see cref="TlsClientRouting" /> sends to it. It verifies the server's
/// chain with the same <see cref="ServerCertificateVerification" />
/// <see cref="SslStreamTlsProvider" /> uses and reports the same events, warnings and failures,
/// so a user cannot tell which client ran.
/// </summary>
/// <remarks>
/// <para>
/// A range that reaches TLS 1.3 from below runs <see cref="TlsClientConnection" />, one
/// ClientHello offering TLS 1.3 and TLS 1.2 down to the minimum (TLS 1.2 when none is
/// given), continued on the version the server picks (ADR-0205); a TLS 1.3 minimum runs
/// <see cref="Tls13ClientConnection" />; a range whose ceiling is TLS 1.2, 1.1 or 1.0 runs
/// <see cref="Tls12ClientConnection" /> offering every version from the minimum (TLS 1.0
/// when none is given) to the ceiling (ADR-0162). When the cipher options leave no suite
/// the client can run for one side of a spanning range, the other side runs alone. The
/// ClientHello carries the target host in <c>server_name</c> unless it is an IP address,
/// the connection's protocols through ALPN unless <c>--no-alpn</c>, and the <c>--cert</c>
/// certificate when the server asks for one and its key is RSA or ECDSA.
/// </para>
/// <para>
/// Failures are the <see cref="SslStreamTlsProvider" />'s: exit 58 or 43 for a
/// <c>--cert</c> that does not load, 59 for <c>--ciphers</c> the build refuses or cannot
/// apply, 77 for an unusable <c>--cacert</c>, 60 (or the Schannel build's 35 for an expired
/// certificate) for a certificate that does not verify, and 35 for any other handshake
/// failure, with the text <see cref="TlsFailureMessages" /> gives each build. With
/// <c>--cert-status</c> it also asks for the server's stapled OCSP response, and one that
/// does not vouch for the certificate is exit 91 on every platform, with the text
/// <see cref="CertificateStatusFailureMessages" /> gives (ADR-0191).
/// </para>
/// </remarks>
public sealed class HandBuiltTlsProvider : IHandshakeReportingTlsProvider, ITlsProviderWithWarnings
{
    /// <inheritdoc />
    TlsClientRoute IHandshakeReportingTlsProvider.Route => TlsClientRoute.HandBuilt;

    /// <inheritdoc />
    string? IHandshakeReportingTlsProvider.RouteReason => TlsClientRouting.Reason(_options);

    private readonly TlsClientOptions _options;

    private readonly bool _matchesSchannelBuild;

    private readonly TimeProvider _timeProvider;

    private readonly IClientCertificateStore _certificateStore;

    private readonly ITlsRandomSource _random;

    private readonly ServerCertificateVerification _verification;

    private readonly TlsSessionCache? _sessions;

    private readonly IEchConfigListLookup? _echConfigs;

    /// <summary>
    /// Creates the provider for the curl build this platform usually runs: Schannel on
    /// Windows, OpenSSL elsewhere.
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    /// <param name="timeProvider">Takes the timestamps in a successful handshake's timings.</param>
    public HandBuiltTlsProvider(TlsClientOptions options, TimeProvider timeProvider)
        : this(options, OperatingSystem.IsWindows(), timeProvider, new SystemClientCertificateStore(), SystemTlsRandomSource.Instance)
    {
    }

    /// <summary>
    /// Creates the provider for the curl build this platform usually runs, offering each TLS
    /// 1.3 handshake a session from <paramref name="sessions" /> and keeping the session
    /// tickets it receives there (the run's cache, which <c>--ssl-sessions</c> also loads and saves,
    /// ADR-0319; none under <c>--no-sessionid</c>, BL-713), and finding a
    /// host's ECHConfigList for <c>--ech true</c> or <c>hard</c> without <c>ecl:</c> through
    /// <paramref name="echConfigs" /> (ADR-0327).
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    /// <param name="timeProvider">Takes the timestamps in a successful handshake's timings.</param>
    /// <param name="sessions">The run's session cache, or <see langword="null" /> to neither offer nor keep sessions.</param>
    /// <param name="echConfigs">Finds a host's ECHConfigList through DoH, or <see langword="null" /> when no DoH server is used.</param>
    public HandBuiltTlsProvider(TlsClientOptions options, TimeProvider timeProvider, TlsSessionCache? sessions, IEchConfigListLookup? echConfigs)
        : this(options, OperatingSystem.IsWindows(), timeProvider, new SystemClientCertificateStore(), SystemTlsRandomSource.Instance, sessions, echConfigs)
    {
    }

    /// <summary>
    /// Creates the provider for a named curl build, so either build's behaviour can be tested
    /// on any platform.
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    /// <param name="matchesSchannelBuild">
    /// <see langword="true" /> to behave like curl's Schannel build, <see langword="false" />
    /// like its OpenSSL build.
    /// </param>
    /// <param name="timeProvider">Takes the timestamps in a successful handshake's timings.</param>
    /// <param name="certificateStore">Opens the store a Schannel <c>--cert</c> store path names.</param>
    /// <param name="random">The source of the client's randoms and key shares.</param>
    /// <param name="sessions">The run's session cache, or <see langword="null" /> to neither offer nor keep sessions.</param>
    /// <param name="echConfigs">Finds a host's ECHConfigList for <c>--ech</c>, or <see langword="null" /> when no DoH server is used.</param>
    /// <exception cref="ArgumentException">
    /// <see cref="TlsClientOptions.MinimumVersion" /> is above <see cref="TlsClientOptions.MaximumVersion" />.
    /// </exception>
    internal HandBuiltTlsProvider(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        TimeProvider timeProvider,
        IClientCertificateStore certificateStore,
        ITlsRandomSource random,
        TlsSessionCache? sessions = null,
        IEchConfigListLookup? echConfigs = null)
    {
        _sessions = sessions;
        _echConfigs = echConfigs;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ = TlsVersionRange.ToSslProtocols(options.MinimumVersion, options.MaximumVersion);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _matchesSchannelBuild = matchesSchannelBuild;
        _verification = new ServerCertificateVerification(options, matchesSchannelBuild, timeProvider);
        Warnings = SslStreamTlsProvider.WarningsFor(options, matchesSchannelBuild);
    }

    /// <summary>
    /// Gets the lines curl writes to standard error for options this build ignores, as
    /// <see cref="SslStreamTlsProvider.Warnings" /> gives them.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    // A range that reaches TLS 1.3 offers it, and one whose minimum is below TLS 1.3 offers
    // TLS 1.2 and below; a range doing both offers them in one ClientHello (ADR-0162, ADR-0205).
    private bool OffersTls13 => _options.MaximumVersion is TlsVersion.SystemDefault or TlsVersion.Tls13;

    private bool OffersBelowTls13 => _options.MinimumVersion is not TlsVersion.Tls13;

    // The platform curl's measured ClientHello (ADR-0140, "Default ClientHello"; BL-820).
    private ClientHelloProfile Profile => _matchesSchannelBuild ? ClientHelloProfile.Schannel : ClientHelloProfile.OpenSsl;

    private bool OffersOnlyVersionsBelowTls12 => _options.MaximumVersion is TlsVersion.Tls10 or TlsVersion.Tls11;

    // OpenSSL 3's default security level allows no version below TLS 1.2, so the OpenSSL
    // build has nothing to offer under a lower ceiling (measured, BL-1152, ADR-0364).
    private bool RefusesItsVersionRange => !_matchesSchannelBuild && OffersOnlyVersionsBelowTls12;

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, NoTransferEvents.Instance, cancellationToken);

    /// <summary>
    /// Performs the client handshake and, when it succeeds, reports a
    /// <see cref="TlsHandshakeEvent" /> to <paramref name="events" />.
    /// </summary>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the trust and the completed handshake are reported.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result <see cref="ITlsProvider.AuthenticateAsClientAsync(IConnection, string, CancellationToken)" /> describes.</returns>
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, events, isProxy: false, [], cancellationToken);

    /// <summary>
    /// Performs the client handshake, offering <paramref name="applicationProtocols" /> through
    /// ALPN unless <see cref="TlsClientOptions.UseAlpn" /> is off, and reports to
    /// <paramref name="events" /> a <see cref="TlsTrustEvent" /> before the handshake and a
    /// <see cref="TlsHandshakeEvent" /> when it succeeds, as <see cref="SslStreamTlsProvider" />
    /// reports them, with the key-exchange group and peer signature type left
    /// <see langword="null" /> as that provider leaves them.
    /// </summary>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the trust and the completed handshake are reported.</param>
    /// <param name="isProxy"><see langword="true" /> when the handshake is with an HTTPS proxy.</param>
    /// <param name="applicationProtocols">The protocols to offer through ALPN, in preference order.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result <see cref="ITlsProvider.AuthenticateAsClientAsync(IConnection, string, CancellationToken)" /> describes.</returns>
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        bool isProxy,
        IReadOnlyList<string> applicationProtocols,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);
        ArgumentNullException.ThrowIfNull(applicationProtocols);

        var offeredApplicationProtocols = OfferedApplicationProtocols(applicationProtocols);
        var (prepared, preparationFailure, alertRecord) = await PrepareWithEchAsync(events, targetHost, offeredApplicationProtocols, plaintext.RemoteEndPoint, cancellationToken).ConfigureAwait(false);
        if (prepared is null)
        {
            return await FailBeforeHandshakeAsync(plaintext, preparationFailure!, alertRecord, cancellationToken).ConfigureAwait(false);
        }

        var peerKey = SessionPeerKey(targetHost, plaintext.RemoteEndPoint);
        var session = OfferedSession(peerKey);
        ReportReusedSession(events, session);
        prepared = prepared with { Settings = prepared.Settings with { ResumptionSession = session } };
        var handshakeStarted = _timeProvider.GetTimestamp();
        var handshakeRun = new HandshakeRun(plaintext, targetHost, events, isProxy, prepared, offeredApplicationProtocols, peerKey);
        if (EarlyDataApplicationProtocol(session, offeredApplicationProtocols) is { } earlyDataProtocol)
        {
            return DeferHandshake(handshakeRun, session!, earlyDataProtocol, handshakeStarted);
        }

        var (handshake, failure) = await CompleteHandshakeAsync(handshakeRun, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
        return failure ?? ConnectResult.Connected(
            ConnectionOver(handshake!, handshakeRun),
            new ConnectTimings(handshakeStarted, null, handshakeStarted, _timeProvider.GetTimestamp()),
            peerCertificates: prepared.Verifier.PeerCertificates,
            applicationProtocol: handshake!.ApplicationProtocol);
    }

    // Runs the prepared handshake, sending earlyData first (as 0-RTT early data when the hello
    // offers it), and reports it as a connect does: the verify result, then on success the
    // handshake and the stapled status; a failure disposes the plaintext and is the result.
    private async ValueTask<(HandBuiltHandshake? Handshake, ConnectResult? Failure)> CompleteHandshakeAsync(
        HandshakeRun run,
        ReadOnlyMemory<byte> earlyData,
        CancellationToken cancellationToken)
    {
        var (handshake, thrown) = await TryHandshakeAsync(run.Plaintext, run.Prepared, earlyData, cancellationToken).ConfigureAwait(false);
        run.Prepared.Verifier.Observed.ReportVerifyResult(run.Events, run.IsProxy, _matchesSchannelBuild);
        var failedHandshakeReported = !Completed(handshake) && ReportFailedHandshake(run);
        run.Prepared.Verifier.Observed.ReportPinnedPublicKeyRefusal(run.Events, _matchesSchannelBuild, failedHandshakeReported);
        if (!Completed(handshake))
        {
            return (null, await FailAsync(run, handshake?.Failure, thrown).ConfigureAwait(false));
        }

        KeepReceivedSessions(run.PeerKey, handshake.Stream!);
        run.Events.ReportTlsHandshake(DescribeHandshake(handshake, run.Prepared.Verifier, run.OfferedApplicationProtocols) with
        {
            IsProxy = run.IsProxy,
            VerifiedHostName = SslStreamTlsProvider.VerifiedHostName(run.TargetHost, _options.Insecure),
            EchResult = EchResultText.Of(_options, run.Prepared.Settings.EchConfigs, run.TargetHost, handshake.EchRetryConfigs),
            EchRetryConfigLines = EchRetryConfigsText.Grease(handshake.EchRetryConfigs),
        });
        CertificateStatusText.Report(run.Events, handshake.CertificateStatus);
        return (handshake, null);
    }

    // The Schannel build prints its ALPN offer, and a refused pin's hash, before a failed
    // handshake (ADR-0363, BL-1149). The OpenSSL build's lines need the version and suite a
    // failed hand-built handshake does not keep, so it reports the hash line alone, as before.
    private bool ReportFailedHandshake(HandshakeRun run)
    {
        if (!_matchesSchannelBuild)
        {
            return false;
        }

        run.Events.ReportTlsHandshake(new TlsHandshakeEvent
        {
            ProtocolVersion = SslProtocols.None,
            CipherSuite = null,
            NegotiatedApplicationProtocol = null,
            OfferedApplicationProtocols = run.OfferedApplicationProtocols,
            ServerCertificate = null,
            CertificateVerified = false,
            PinnedPublicKeyHash = run.Prepared.Verifier.Observed.PinnedPublicKeyHash,
            IsProxy = run.IsProxy,
            Failed = true,
        });
        return true;
    }

    private HandBuiltTlsConnection ConnectionOver(HandBuiltHandshake handshake, HandshakeRun run) =>
        new(handshake.Stream!, run.Plaintext, run.Prepared.ClientCertificate, TlsFailureMessages.MissingCloseNotify(_matchesSchannelBuild), !_matchesSchannelBuild)
        {
            TicketEvents = _matchesSchannelBuild ? run.Events : null,
        };

    // The OpenSSL build's line for a session it offers (openssl.c, after SSL_set_session; BL-1142),
    // with the session's ALPN protocol or '-', as measured; the Schannel build prints none.
    private void ReportReusedSession(ITransferEvents events, TlsSessionRecord? session)
    {
        if (session is not null && !_matchesSchannelBuild)
        {
            events.ReportInfo($"SSL reusing session with ALPN '{session.ApplicationProtocol ?? "-"}'");
        }
    }

    // --tls-earlydata (BL-1105): the ALPN protocol of a resumed TLS 1.3 session that allows
    // early data, when the connection offers it, as curl's Curl_on_session_reuse decides; else null.
    private string? EarlyDataApplicationProtocol(TlsSessionRecord? session, IReadOnlyList<string> offeredApplicationProtocols) =>
        _options.AllowEarlyData && OffersTls13 && EarlyDataProtocolOf(session) is { } protocol && offeredApplicationProtocols.Contains(protocol)
            ? protocol
            : null;

    // The ALPN protocol of a session that allows early data, or null; the cache holds only TLS 1.3 sessions.
    private static string? EarlyDataProtocolOf(TlsSessionRecord? session) =>
        session is { MaxEarlyDataSize: > 0 } ? session.ApplicationProtocol : null;

    // curl's deferred connect (vtls.c, ssl_connection_deferred): the connection is up at once
    // with the session's ALPN protocol, the only one the hello then offers, and the handshake
    // runs on the first write, carrying it as 0-RTT early data.
    private ConnectResult DeferHandshake(HandshakeRun run, TlsSessionRecord session, string protocol, long handshakeStarted)
    {
        run.Events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"SSL session allows {session.MaxEarlyDataSize} bytes of early data, reusing ALPN '{protocol}'"));
        var deferred = run with
        {
            Prepared = run.Prepared with { Settings = run.Prepared.Settings with { ApplicationProtocols = [protocol], OfferEarlyData = true } },
        };
        return ConnectResult.Connected(
            new EarlyDataTlsConnection(run.Plaintext, (earlyData, cancellationToken) => HandshakeWithEarlyDataAsync(deferred, session.MaxEarlyDataSize, earlyData, cancellationToken)),
            new ConnectTimings(handshakeStarted, null, handshakeStarted, _timeProvider.GetTimestamp()),
            applicationProtocol: protocol);
    }

    // The deferred handshake, with curl's early data lines (openssl.c's ossl_send_earlydata,
    // vtls.c's ssl_cf_connect_deferred); a failure is the connect's exit code and message.
    private async ValueTask<IConnection> HandshakeWithEarlyDataAsync(HandshakeRun run, uint maxEarlyDataSize, ReadOnlyMemory<byte> earlyData, CancellationToken cancellationToken)
    {
        var sent = (int)Math.Min((uint)earlyData.Length, maxEarlyDataSize);
        run.Events.ReportInfo(string.Create(CultureInfo.InvariantCulture, $"SSL sending {sent} bytes of early data"));
        var (handshake, failure) = await CompleteHandshakeAsync(run, earlyData, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            throw new DeferredTlsHandshakeFailedException(failure.ExitCode, failure.ErrorMessage!);
        }

        var accepted = EarlyDataAccepted(handshake!.Stream!);
        ReportEarlyDataSent(run, accepted ? sent : -sent);
        run.Events.ReportInfo(accepted
            ? string.Create(CultureInfo.InvariantCulture, $"Server accepted {sent} bytes of TLS early data.")
            : "Server rejected TLS early data.");
        return ConnectionOver(handshake, run);
    }

    // %{tls_earlydata} (BL-906): openssl.c's Curl_pgrsEarlyData call, the bytes sent and
    // negative when rejected, made only for the origin's connection, never a proxy's.
    private static void ReportEarlyDataSent(HandshakeRun run, long bytes)
    {
        if (!run.IsProxy)
        {
            run.Events.ReportTlsEarlyData(bytes);
        }
    }

    // Whether the server accepted the early data; a TLS 1.2 connection accepts none.
    internal static bool EarlyDataAccepted(Stream stream) =>
        stream is Tls13ClientStream { Handshake.EarlyDataAccepted: true };

    // One connection's handshake: what it runs over, for whom, and what it reports.
    private sealed record HandshakeRun(
        IConnection Plaintext,
        string TargetHost,
        ITransferEvents Events,
        bool IsProxy,
        PreparedHandshake Prepared,
        IReadOnlyList<string> OfferedApplicationProtocols,
        string? PeerKey);

    private IReadOnlyList<string> OfferedApplicationProtocols(IReadOnlyList<string> applicationProtocols) =>
        _options.UseAlpn ? applicationProtocols : [];

    private static bool Completed([NotNullWhen(true)] HandBuiltHandshake? handshake) => handshake is { Failure: null };

    // Prepare, then the --ech offer (ADR-0327): --ech hard with no usable configuration fails
    // here, before a byte is sent, and the --cert certificate is disposed.
    private async ValueTask<(PreparedHandshake? Prepared, ConnectResult? Failure, byte[]? AlertRecord)> PrepareWithEchAsync(
        ITransferEvents events,
        string targetHost,
        IReadOnlyList<string> offeredApplicationProtocols,
        EndPoint? remoteEndPoint,
        CancellationToken cancellationToken)
    {
        var preparation = Prepare(events, targetHost, offeredApplicationProtocols);
        if (preparation.Prepared is not { } prepared)
        {
            return preparation;
        }

        var echOffer = await EchOffer.DecideAsync(_options, OffersTls13, _echConfigs, targetHost, PortOf(remoteEndPoint), cancellationToken).ConfigureAwait(false);
        foreach (var line in echOffer.InfoLines)
        {
            events.ReportInfo(line);
        }

        if (echOffer.Failure is not null)
        {
            prepared.ClientCertificate?.Dispose();
            return (null, echOffer.Failure, null);
        }

        return (prepared with { Settings = prepared.Settings with { EchConfigs = echOffer.Configs, SendEchGrease = echOffer.SendGrease } }, null, null);
    }

    /// <summary>
    /// Turns the <c>--cert</c> certificate into what the hand-built client presents: the
    /// certificate and a signing key for its RSA or ECDSA private key, or the Ed25519, Ed448
    /// or ML-DSA signing key a <see cref="HandBuiltKeyCertificate" /> carries.
    /// </summary>
    /// <param name="certificate">The loaded certificate with its key, or <see langword="null" />.</param>
    /// <returns>The client certificate, or <see langword="null" /> when there is none or its key is of another kind.</returns>
    internal static TlsClientCertificate? ToTlsClientCertificate(X509Certificate2? certificate) =>
        certificate is not null && SigningKeyOf(certificate) is { } key
            ? new TlsClientCertificate([certificate.RawData], key)
            : null;

    private static TlsSigningKey? SigningKeyOf(X509Certificate2 certificate) =>
        certificate is HandBuiltKeyCertificate handBuilt ? handBuilt.SigningKey
            : certificate.GetRSAPrivateKey() is { } rsa ? new RsaTlsSigningKey(rsa)
            : certificate.GetECDsaPrivateKey() is { } ecdsa ? new EcdsaTlsSigningKey(ecdsa)
            : null;

    // The connection's peer key in the run's session cache, or null when there is no cache or,
    // under --no-sessionid, sessions are neither offered nor kept (BL-713).
    private string? SessionPeerKey(string targetHost, EndPoint? remoteEndPoint) =>
        _sessions is null || _options.NoSessionId ? null : TlsSessionCache.PeerKey(targetHost, PortOf(remoteEndPoint), _options);

    // The session the ClientHello offers to resume: taken out of the cache, as curl takes a TLS 1.3 one.
    private TlsSessionRecord? OfferedSession(string? peerKey) => peerKey is null ? null : _sessions!.Take(peerKey);

    // Keeps the session tickets the connection receives, read when the cache is saved.
    private void KeepReceivedSessions(string? peerKey, Stream stream)
    {
        if (peerKey is not null)
        {
            _sessions!.Track(peerKey, () => ReceivedSessionsOf(stream));
        }
    }

    // Whether a TLS 1.0 CBC write is preceded by OpenSSL's empty application data record (ADR-0150):
    // always, unless --ssl-allow-beast or --proxy-ssl-allow-beast turns the split off (BL-713).
    internal static bool InsertsEmptyFragment(TlsClientOptions options) => !options.AllowBeast;

    // The port the session cache's peer key names: the connection's, or https's when the
    // connection does not know its address.
    internal static int PortOf(EndPoint? remoteEndPoint) => remoteEndPoint is IPEndPoint address ? address.Port : 443;

    // The session tickets a TLS 1.3 connection has received; the TLS 1.2 client keeps none.
    internal static IReadOnlyList<TlsSessionRecord> ReceivedSessionsOf(Stream stream) =>
        stream is Tls13ClientStream tls13 ? tls13.Handshake.ReceivedSessions : [];

    /// <summary>
    /// Returns the name the ClientHello carries in <c>server_name</c>: the target host, or
    /// <see langword="null" /> for an IP address, which RFC 6066 section 3 leaves out.
    /// </summary>
    /// <param name="targetHost">The host the handshake is with, an IPv6 literal in brackets or not.</param>
    /// <returns>The server name, or <see langword="null" />.</returns>
    internal static string? ServerNameFor(string targetHost) =>
        IPAddress.TryParse(targetHost.Trim('[', ']'), out _) ? null : targetHost;

    /// <summary>Loads the server's own certificate for the handshake event.</summary>
    /// <param name="peerCertificates">The DER of what the server sent, its own first.</param>
    /// <returns>The certificate, or <see langword="null" /> when the server sent none.</returns>
    internal static X509Certificate2? ServerCertificateOf(ReadOnlyMemory<byte>[] peerCertificates) =>
        peerCertificates is [var serverCertificate, ..] ? X509CertificateLoader.LoadCertificate(serverCertificate.Span) : null;

    private static TlsHandshakeEvent DescribeHandshake(
        HandBuiltHandshake handshake,
        HandBuiltCertificateVerifier verifier,
        IReadOnlyList<string> offeredApplicationProtocols) => new()
        {
            ProtocolVersion = handshake.ProtocolVersion,
            CipherSuite = (TlsCipherSuite)handshake.CipherSuite,
            NegotiatedApplicationProtocol = handshake.ApplicationProtocol,
            OfferedApplicationProtocols = offeredApplicationProtocols,
            ServerCertificate = ServerCertificateOf(verifier.PeerCertificates),
            CertificateVerified = verifier.Observed.Verified,
            CertificateVerifyResult = verifier.Observed.VerifyResult,
            PeerCertificateChain = [.. verifier.Observed.Chain.Select(der => X509CertificateLoader.LoadCertificate(der.Span))],
            PinnedPublicKeyHash = verifier.Observed.PinnedPublicKeyHash,
        };

    /// <summary>
    /// The fatal <c>internal_error</c> alert OpenSSL writes when <c>--curves</c> or
    /// <c>--sigalgs</c> leaves nothing to offer, in a record with the ClientHello's legacy
    /// version 3.1 (measured 2026-10-01 with curl 8.18.0 and OpenSSL 3.5.5, BL-1087).
    /// </summary>
    internal static ReadOnlySpan<byte> InternalErrorAlertRecord => [0x15, 0x03, 0x01, 0x00, 0x02, 0x02, 0x50];

    // A --curves or --sigalgs failure sends the internal_error alert only when nothing is left
    // to offer (exit 35); a refused list (exit 59) sends nothing.
    private static byte[]? InternalErrorAlertFor(ConnectResult listFailure) =>
        listFailure.ExitCode == CurlExitCode.SslConnectError ? InternalErrorAlertRecord.ToArray() : null;

    /// <summary>
    /// The fatal <c>protocol_version</c> alert OpenSSL writes in place of a ClientHello when
    /// <c>--tls-max 1.0</c> or <c>1.1</c> leaves no version to offer, in a record of version
    /// 3.3 (measured 2026-10-02 with curl 8.18.0 and OpenSSL 3.5.5, BL-1152).
    /// </summary>
    internal static ReadOnlySpan<byte> ProtocolVersionAlertRecord => [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x46];

    // A failure found before the handshake: the alert when it calls for one, then the
    // plaintext is disposed whatever the write did, and cancellation escapes as in FailAsync.
    private static async ValueTask<ConnectResult> FailBeforeHandshakeAsync(IConnection plaintext, ConnectResult failure, byte[]? alertRecord, CancellationToken cancellationToken)
    {
        var thrown = alertRecord is not null ? await TrySendAlertAsync(plaintext, alertRecord, cancellationToken).ConfigureAwait(false) : null;
        await plaintext.DisposeAsync().ConfigureAwait(false);
        RethrowIfCancellation(thrown);
        return failure;
    }

    // The alert is a courtesy: a peer that is already gone leaves the failure as it was.
    private static async ValueTask<Exception?> TrySendAlertAsync(IConnection plaintext, byte[] alertRecord, CancellationToken cancellationToken)
    {
        try
        {
            await plaintext.WriteAsync(alertRecord, cancellationToken).ConfigureAwait(false);
            await plaintext.FlushAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    // Cancellation is the one exception ITlsProvider lets escape; its stack trace is kept.
    private static void RethrowIfCancellation(Exception? thrown)
    {
        if (thrown is not OperationCanceledException)
        {
            return;
        }

        ExceptionDispatchInfo.Throw(thrown);
    }

    // A handshake that failed or threw: the plaintext and the --cert certificate are
    // disposed, cancellation escapes, and anything else is the build's failure.
    private async ValueTask<ConnectResult> FailAsync(HandshakeRun run, TlsHandshakeFailure? failure, Exception? thrown)
    {
        run.Prepared.ClientCertificate?.Dispose();
        await run.Plaintext.DisposeAsync().ConfigureAwait(false);
        RethrowIfCancellation(thrown);
        CertificateStatusText.Report(run.Events, failure?.CertificateStatusRejection);
        ReportEchRejection(run, failure);
        return thrown is null
            ? FailedHandshake(failure!)
            : ConnectResult.Failed(CurlExitCode.SslConnectError, SslConnectError(thrown));
    }

    // A rejected ECH offer: curl traces the server's retry_configs before its failf, or
    // says there were none (measured with OpenSSL 4.0.0, ADR-0359, BL-1171).
    private static void ReportEchRejection(HandshakeRun run, TlsHandshakeFailure? failure)
    {
        if (failure?.Alert != TlsAlertDescription.EchRequired)
        {
            return;
        }

        foreach (var line in EchRetryConfigsText.Rejected(failure.EchRetryConfigs, run.TargetHost, run.Prepared.Settings.EchConfigs!.SupportedConfig!.PublicName))
        {
            run.Events.ReportInfo(line);
        }
    }

    // Everything the handshake needs before a byte is sent, in the order the SslStream
    // provider does it: the suites, the --cert certificate, the trust event, the anchors.
    // --curves or --sigalgs leaving nothing to offer (exit 35, not a refused list's 59) is
    // the one failure OpenSSL announces with an internal_error alert (BL-1087).
    private (PreparedHandshake? Prepared, ConnectResult? Failure, byte[]? AlertRecord) Prepare(
        ITransferEvents events,
        string targetHost,
        IReadOnlyList<string> offeredApplicationProtocols)
    {
        var (suites, cipherFailure) = SelectCipherSuites();
        if (cipherFailure is not null)
        {
            return (null, ConnectResult.Failed(CurlExitCode.SslCipher, cipherFailure), null);
        }

        (suites, var srpFailure) = WithSrpSuites(events, suites);
        if (srpFailure is not null)
        {
            return (null, srpFailure, null);
        }

        var (profile, listFailure) = CurvesAndSignatureAlgorithms.Apply(Profile, _options);
        if (listFailure is not null)
        {
            return (null, listFailure, InternalErrorAlertFor(listFailure));
        }

        var (clientCertificate, clientCertificateFailure) = ClientCertificateLoader.Load(_options, _matchesSchannelBuild, _certificateStore, _timeProvider.GetUtcNow());
        if (clientCertificateFailure is not null)
        {
            SslStreamTlsProvider.ReportTrustBeforeClientCertificateFailure(events, _options, targetHost, _matchesSchannelBuild);
            return (null, clientCertificateFailure, null);
        }

        events.ReportTlsTrust(SslStreamTlsProvider.DescribeTrust(_options, targetHost));
        return PrepareTrust(targetHost, offeredApplicationProtocols, suites, profile!, clientCertificate);
    }

    // The OpenSSL build's refusal of a legacy ceiling (BL-1152), then the trust anchors; a
    // failure disposes the --cert certificate.
    private (PreparedHandshake? Prepared, ConnectResult? Failure, byte[]? AlertRecord) PrepareTrust(
        string targetHost,
        IReadOnlyList<string> offeredApplicationProtocols,
        IReadOnlyList<ushort>? suites,
        ClientHelloProfile profile,
        X509Certificate2? clientCertificate)
    {
        if (RefusesItsVersionRange)
        {
            clientCertificate?.Dispose();
            return (null, ConnectResult.Failed(CurlExitCode.SslConnectError, TlsFailureMessages.OpenSslNoProtocolsAvailable), ProtocolVersionAlertRecord.ToArray());
        }

        try
        {
            var (chainPolicy, anchorsBesideSystemStore, revocationLists) = _verification.ReadTrustAnchors();
            return (new PreparedHandshake(
                ClientSettings.Of(targetHost, offeredApplicationProtocols, suites, ToTlsClientCertificate(clientCertificate), profile) with
                {
                    RequestOcspStatus = _options.RequireCertificateStatus,
                    TimeProvider = _timeProvider,
                    SrpCredentials = TlsSrp.CredentialsOf(_options),
                },
                clientCertificate,
                new HandBuiltCertificateVerifier(_verification, chainPolicy, anchorsBesideSystemStore, revocationLists, targetHost)), null, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            clientCertificate?.Dispose();
            var (exitCode, message) = _verification.TrustAnchorsUnusable(exception);
            return (null, ConnectResult.Failed(exitCode, message), null);
        }
    }

    // --tlsuser: announce the login and offer OpenSSL's SRP cipher list, or fail with exit 43
    // when no --tlspassword came with it, as curl's OpenSSL build does (ADR-0328).
    private (IReadOnlyList<ushort>? Suites, ConnectResult? Failure) WithSrpSuites(ITransferEvents events, IReadOnlyList<ushort>? suites)
    {
        if (_options.TlsUser is null)
        {
            return (suites, null);
        }

        TlsSrp.ReportUser(events, _options);
        if (_options.TlsPassword is null)
        {
            return (null, ConnectResult.Failed(CurlExitCode.BadFunctionArgument, TlsSrp.PasswordMissing));
        }

        TlsSrp.ReportCipherList(events, _options);
        return (TlsSrp.OfferedSuites(_options, suites ?? Profile.CipherSuites), null);
    }

    // The handshake's outcome, or what it threw: the transport's failures and cancellation.
    private async Task<(HandBuiltHandshake? Handshake, Exception? Thrown)> TryHandshakeAsync(
        IConnection plaintext,
        PreparedHandshake prepared,
        ReadOnlyMemory<byte> earlyData,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await HandshakeAsync(new ConnectionStream(plaintext), prepared.Settings, prepared.Verifier, earlyData, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    // A certificate the verifier rejected fails as the SslStream provider fails it, a stapled
    // OCSP response --cert-status rejected is exit 91 (ADR-0191), a server that did not accept
    // the ECH offer is exit 101 (ADR-0327), and any other handshake failure is exit 35.
    private ConnectResult FailedHandshake(TlsHandshakeFailure failure) =>
        failure.Alert == TlsAlertDescription.EchRequired
            ? ConnectResult.Failed(CurlExitCode.EchRequired, TlsFailureMessages.EchRequired)
            : failure.CertificateRejection is ValueTuple<CurlExitCode, string> rejected
            ? ConnectResult.Failed(rejected.Item1, rejected.Item2)
            : failure.CertificateStatusRejection is { } statusRejection
            ? ConnectResult.Failed(CurlExitCode.SslInvalidCertStatus, CertificateStatusFailureMessages.For(statusRejection))
            : ConnectResult.Failed(CurlExitCode.SslConnectError, HandshakeFailureMessage(failure));

    private async Task<HandBuiltHandshake> HandshakeAsync(
        Stream transport,
        ClientSettings settings,
        IServerCertificateVerifier verifier,
        ReadOnlyMemory<byte> earlyData,
        CancellationToken cancellationToken)
    {
        // Every TLS 1.3 suite is runnable (BL-811), and a --tls13-ciphers list naming none
        // is exit 59 before this, so a range reaching TLS 1.3 always offers it.
        var runsTls13 = OffersTls13;
        var runsTls12 = OffersBelowTls13 && settings.OffersSuiteFor(IsTls12Suite);
        return runsTls13 && runsTls12 ? await HandshakeTls13OrTls12Async(transport, settings, verifier, earlyData, cancellationToken).ConfigureAwait(false)
            : runsTls13 ? await HandshakeTls13Async(transport, settings, verifier, earlyData, cancellationToken).ConfigureAwait(false)
            : await HandshakeTls12Async(transport, settings, verifier, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsTls12Suite(ushort cipherSuite) => Tls12CipherSuite.Find(cipherSuite) is not null;

    private static HandBuiltHandshake Describe(TlsConnectResult result) =>
        result.Tls13Stream is { } tls13Stream ? HandBuiltHandshake.Completed(tls13Stream)
            : result.Tls12Stream is { } tls12Stream ? HandBuiltHandshake.Completed(tls12Stream)
            : HandBuiltHandshake.Failed(result.Failure!);

    // One ClientHello offering both, continued on the version the ServerHello picks (BL-821).
    private async Task<HandBuiltHandshake> HandshakeTls13OrTls12Async(
        Stream transport,
        ClientSettings settings,
        IServerCertificateVerifier verifier,
        ReadOnlyMemory<byte> earlyData,
        CancellationToken cancellationToken)
    {
        var offer = new TlsClientSettings(settings.ToTls13(alongsideTls12: true), settings.ToTls12(_options));
        return Describe(await TlsClientConnection.ConnectWithEarlyDataAsync(transport, offer, _random, verifier, earlyData, cancellationToken).ConfigureAwait(false));
    }

    private async Task<HandBuiltHandshake> HandshakeTls13Async(
        Stream transport,
        ClientSettings settings,
        IServerCertificateVerifier verifier,
        ReadOnlyMemory<byte> earlyData,
        CancellationToken cancellationToken)
    {
        var tls13 = await Tls13ClientConnection.ConnectWithEarlyDataAsync(transport, settings.ToTls13(alongsideTls12: false), _random, verifier, earlyData, cancellationToken).ConfigureAwait(false);
        return tls13.Stream is { } tls13Stream ? HandBuiltHandshake.Completed(tls13Stream) : HandBuiltHandshake.Failed(tls13.Failure!);
    }

    private async Task<HandBuiltHandshake> HandshakeTls12Async(
        Stream transport,
        ClientSettings settings,
        IServerCertificateVerifier verifier,
        CancellationToken cancellationToken)
    {
        var tls12 = await Tls12ClientConnection.ConnectAsync(transport, settings.ToTls12(_options), _random, verifier, cancellationToken).ConfigureAwait(false);
        return tls12.Stream is { } tls12Stream ? HandBuiltHandshake.Completed(tls12Stream) : HandBuiltHandshake.Failed(tls12.Failure!);
    }

    // ADR-0011 as SslStreamTlsProvider applies it: the Schannel build refuses --ciphers and
    // ignores --tls13-ciphers; the OpenSSL build offers what they name that the client can
    // run, and a list naming none of those is exit 59.
    private (IReadOnlyList<ushort>? Suites, string? FailureMessage) SelectCipherSuites() =>
        _matchesSchannelBuild
            ? (null, _options.Ciphers is null ? null : TlsFailureMessages.SchannelCipherListRefused)
            : SelectOpenSslCipherSuites();

    private (IReadOnlyList<ushort>? Suites, string? FailureMessage) SelectOpenSslCipherSuites()
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select(_options.Ciphers, _options.Tls13Ciphers);
        if (suites is null)
        {
            return (null, failureMessage);
        }

        ushort[] offered = [.. suites.Select(suite => (ushort)suite).Where(CanOffer)];
        return offered.Length == 0
            ? (null, OpenSslCipherSuites.Unapplied(_options.Ciphers, _options.Tls13Ciphers))
            : (offered, null);
    }

    // Whether a client the range runs can protect records with the suite.
    private bool CanOffer(ushort cipherSuite) =>
        (OffersTls13 && Tls13RecordProtection.CanProtect(cipherSuite)) || (OffersBelowTls13 && IsTls12Suite(cipherSuite));

    private string SslConnectError(Exception failure) => _matchesSchannelBuild
        ? TlsFailureMessages.SchannelSslConnectError(failure, OffersOnlyVersionsBelowTls12)
        : TlsFailureMessages.OpenSslSslConnectError(failure);

    // The Schannel build ignores --sigalgs and --curves, so on Windows a handshake that fails
    // with either in force prints what the build that applies it prints (ADR-0151): OpenSSL's
    // text for --sigalgs and --tlsuser (TLS-SRP, ADR-0328), and for --curves the
    // handshake_failure alert as curl.se's LibreSSL build prints it (ADR-0284).
    private string HandshakeFailureMessage(TlsHandshakeFailure failure) =>
        !_matchesSchannelBuild || _options.SignatureAlgorithms is not null || _options.TlsUser is not null ? TlsFailureMessages.OpenSslHandBuiltHandshakeFailure(failure)
        : _options.Curves is not null && IsHandshakeFailureAlertReceived(failure) ? TlsFailureMessages.LibreSslHandshakeFailureAlert
        : TlsFailureMessages.SchannelHandBuiltHandshakeFailure(failure, OffersOnlyVersionsBelowTls12);

    private static bool IsHandshakeFailureAlertReceived(TlsHandshakeFailure failure) =>
        failure is { Origin: TlsHandshakeFailureOrigin.AlertReceived, Alert: TlsAlertDescription.HandshakeFailure };

    // What a handshake was prepared with: the ClientHello's settings, the --cert certificate
    // the connection disposes, and the verifier that judges the server's chain.
    private sealed record PreparedHandshake(
        ClientSettings Settings,
        X509Certificate2? ClientCertificate,
        HandBuiltCertificateVerifier Verifier);

    // What the ClientHello offers, before the TLS 1.3 or TLS 1.2 client is chosen: the
    // platform curl's profile (BL-820), its suites replaced by the cipher options' when given.
    private sealed record ClientSettings(
        string? ServerName,
        IReadOnlyList<string> ApplicationProtocols,
        IReadOnlyList<ushort>? CipherSuites,
        TlsClientCertificate? ClientCertificate,
        ClientHelloProfile Profile)
    {
        // --cert-status: ask for a stapled OCSP response and judge it on this clock.
        internal bool RequestOcspStatus { get; init; }

        // --ssl-sessions: the session the TLS 1.3 ClientHello offers to resume, if any.
        internal TlsSessionRecord? ResumptionSession { get; init; }

        // --ech: the configurations the TLS 1.3 ClientHello seals its inner hello for, if any.
        internal EchConfigList? EchConfigs { get; init; }

        // --ech grease: a GREASE encrypted_client_hello in the TLS 1.3 ClientHello.
        internal bool SendEchGrease { get; init; }

        // --tls-earlydata: offer early_data on the resumed session (BL-1105).
        internal bool OfferEarlyData { get; init; }

        // --tlsuser and --tlspassword: the TLS-SRP login the TLS 1.2 ClientHello offers (ADR-0229).
        internal TlsSrpCredentials? SrpCredentials { get; init; }

        internal TimeProvider TimeProvider { get; init; } = TimeProvider.System;

        private IReadOnlyList<ushort> OfferedSuites => CipherSuites ?? Profile.CipherSuites;

        internal static ClientSettings Of(
            string targetHost,
            IReadOnlyList<string> applicationProtocols,
            IReadOnlyList<ushort>? cipherSuites,
            TlsClientCertificate? clientCertificate,
            ClientHelloProfile profile) =>
            new(ServerNameFor(targetHost), applicationProtocols, cipherSuites, clientCertificate, profile);

        // Both builds send a 32-byte legacy session ID (middlebox compatibility mode). Beside
        // TLS 1.2, OpenSSL keeps the TLS 1.2-only groups in supported_groups (measured, BL-1086);
        // with TLS 1.3 alone, a group TLS 1.3 cannot use is not offered.
        internal Tls13ClientSettings ToTls13(bool alongsideTls12) => new()
        {
            ServerName = ServerName,
            ApplicationProtocols = ApplicationProtocols,
            ClientCertificate = ClientCertificate,
            RequestOcspStatus = RequestOcspStatus,
            TimeProvider = TimeProvider,
            ResumptionSession = ResumptionSession,
            OfferEarlyData = OfferEarlyData,
            CipherSuites = [.. OfferedSuites.Where(Tls13RecordProtection.CanProtect)],
            SupportedGroups = alongsideTls12 ? Profile.SupportedGroups : [.. Profile.SupportedGroups.Where(TlsNamedGroup.CanShare)],
            KeyShareGroups = Profile.KeyShareGroups,
            SignatureAlgorithms = ClientHelloProfileMapping.CheckableSignatureAlgorithms(Profile),
            CertificateCompressionAlgorithms = Profile.CertificateCompressionAlgorithms,
            ExtensionOrder = WithEncryptedClientHello(WithPadding(WithEarlyData(ClientHelloProfileMapping.ExtensionOrder(Profile, RequestOcspStatus)))),
            FixedExtensions = ClientHelloProfileMapping.FixedExtensions(Profile),
            SendLegacySessionId = true,
            EncryptedClientHelloConfigs = EchConfigs,
            SendEncryptedClientHelloGrease = SendEchGrease,
        };

        // padding follows the measured extensions and early_data, where OpenSSL sends it (BL-1048).
        private IReadOnlyList<TlsExtensionType> WithPadding(IReadOnlyList<TlsExtensionType> order) =>
            Profile.PadsTcpHello ? [.. order, TlsExtensionType.Padding] : order;

        // early_data follows the profile's measured extensions, where OpenSSL sends it (BL-1105).
        private IReadOnlyList<TlsExtensionType> WithEarlyData(IReadOnlyList<TlsExtensionType> order) =>
            OfferEarlyData ? [.. order, TlsExtensionType.EarlyData] : order;

        // encrypted_client_hello goes last, after the profile's measured extensions (ADR-0327).
        private IReadOnlyList<TlsExtensionType> WithEncryptedClientHello(IReadOnlyList<TlsExtensionType> order) =>
            EchConfigs is null && !SendEchGrease ? order : [.. order, TlsExtensionType.EncryptedClientHello];

        // Whether the suites to offer include one the predicate accepts; the profiles always do.
        internal bool OffersSuiteFor(Func<ushort, bool> canProtect) => CipherSuites?.Any(canProtect) ?? true;

        // A range that reaches TLS 1.3 offers TLS 1.2 as its ceiling below it, and with no
        // minimum starts at TLS 1.2, curl's default minimum since 8.10.0 (ADR-0205).
        internal Tls12ClientSettings ToTls12(TlsClientOptions options) => new()
        {
            ServerName = ServerName,
            MinimumVersion = Tls12Minimum(options),
            MaximumVersion = Tls12Maximum(options),
            ClientHelloRecordVersion = Tls12RecordVersion(options),
            ApplicationProtocols = ApplicationProtocols,
            ClientCertificate = ClientCertificate,
            RequestOcspStatus = RequestOcspStatus,
            TimeProvider = TimeProvider,
            CipherSuites = [.. OfferedSuites.Where(IsTls12Suite)],
            SupportedGroups = ClientHelloProfileMapping.Tls12SupportedGroups(Profile),
            SignatureAlgorithms = ClientHelloProfileMapping.Tls12SignatureAlgorithms(Profile),
            OfferSessionTicket = ClientHelloProfileMapping.Sends(Profile, TlsExtensionType.SessionTicket),
            OfferExtendedMasterSecret = ClientHelloProfileMapping.Sends(Profile, TlsExtensionType.ExtendedMasterSecret),
            OfferEncryptThenMac = ClientHelloProfileMapping.Sends(Profile, TlsExtensionType.EncryptThenMac),
            PadHello = Profile.PadsTcpHello,
            ExtensionOrder = ClientHelloProfileMapping.Tls12ExtensionOrder(Profile, RequestOcspStatus),
            FixedExtensions = ClientHelloProfileMapping.Tls12FixedExtensions(Profile),
            SrpCredentials = SrpCredentials,
            InsertEmptyFragment = InsertsEmptyFragment(options),
        };

        // Schannel's hello below a TLS 1.3 ceiling is in a record of that ceiling; a TLS 1.2
        // hello offered beside TLS 1.3, and OpenSSL's, in a TLS 1.0 one (measured, BL-1152).
        private TlsProtocolVersion Tls12RecordVersion(TlsClientOptions options) =>
            Profile.Tls12RecordVersionIsTheCeiling && !ReachesTls13(options) ? Tls12Maximum(options) : TlsProtocolVersion.Tls10;

        private static bool ReachesTls13(TlsClientOptions options) => options.MaximumVersion is TlsVersion.SystemDefault or TlsVersion.Tls13;

        private static TlsProtocolVersion Tls12Minimum(TlsClientOptions options) =>
            ReachesTls13(options) && options.MinimumVersion == TlsVersion.SystemDefault
                ? TlsProtocolVersion.Tls12
                : ToTlsProtocolVersion(options.MinimumVersion);

        private static TlsProtocolVersion Tls12Maximum(TlsClientOptions options) =>
            ReachesTls13(options) ? TlsProtocolVersion.Tls12 : ToTlsProtocolVersion(options.MaximumVersion);

        // Below a TLS 1.2 ceiling an unset minimum is TLS 1.0, as TlsVersionRange offers it.
        private static TlsProtocolVersion ToTlsProtocolVersion(TlsVersion version) => version switch
        {
            TlsVersion.Tls11 => TlsProtocolVersion.Tls11,
            TlsVersion.Tls12 => TlsProtocolVersion.Tls12,
            _ => TlsProtocolVersion.Tls10,
        };
    }
}
