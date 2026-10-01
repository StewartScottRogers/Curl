using System.Net;
using System.Net.Security;
using System.Runtime.ExceptionServices;
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

    private readonly TlsClientOptions _options;

    private readonly bool _matchesSchannelBuild;

    private readonly TimeProvider _timeProvider;

    private readonly IClientCertificateStore _certificateStore;

    private readonly ITlsRandomSource _random;

    private readonly ServerCertificateVerification _verification;

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
    /// <exception cref="ArgumentException">
    /// <see cref="TlsClientOptions.MinimumVersion" /> is above <see cref="TlsClientOptions.MaximumVersion" />.
    /// </exception>
    internal HandBuiltTlsProvider(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        TimeProvider timeProvider,
        IClientCertificateStore certificateStore,
        ITlsRandomSource random)
    {
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

        var offeredApplicationProtocols = _options.UseAlpn ? applicationProtocols : [];
        var (prepared, preparationFailure) = Prepare(events, targetHost, offeredApplicationProtocols);
        if (prepared is null)
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            return preparationFailure!;
        }

        var handshakeStarted = _timeProvider.GetTimestamp();
        var (handshake, thrown) = await TryHandshakeAsync(plaintext, prepared, cancellationToken).ConfigureAwait(false);
        prepared.Verifier.Observed.ReportVerifyResult(events, isProxy, _matchesSchannelBuild);
        if (handshake is not { Failure: null })
        {
            return await FailAsync(plaintext, prepared, handshake?.Failure, thrown).ConfigureAwait(false);
        }

        events.ReportTlsHandshake(DescribeHandshake(handshake, prepared.Verifier, offeredApplicationProtocols) with
        {
            IsProxy = isProxy,
            VerifiedHostName = SslStreamTlsProvider.VerifiedHostName(targetHost, _options.Insecure),
        });
        return ConnectResult.Connected(
            new HandBuiltTlsConnection(handshake.Stream!, plaintext, prepared.ClientCertificate, TlsFailureMessages.MissingCloseNotify(_matchesSchannelBuild)),
            new ConnectTimings(handshakeStarted, null, handshakeStarted, _timeProvider.GetTimestamp()),
            peerCertificates: prepared.Verifier.PeerCertificates,
            applicationProtocol: handshake.ApplicationProtocol);
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
        };

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
    private async ValueTask<ConnectResult> FailAsync(
        IConnection plaintext,
        PreparedHandshake prepared,
        TlsHandshakeFailure? failure,
        Exception? thrown)
    {
        prepared.ClientCertificate?.Dispose();
        await plaintext.DisposeAsync().ConfigureAwait(false);
        RethrowIfCancellation(thrown);
        return thrown is null
            ? FailedHandshake(failure!)
            : ConnectResult.Failed(CurlExitCode.SslConnectError, SslConnectError(thrown));
    }

    // Everything the handshake needs before a byte is sent, in the order the SslStream
    // provider does it: the suites, the --cert certificate, the trust event, the anchors.
    private (PreparedHandshake? Prepared, ConnectResult? Failure) Prepare(
        ITransferEvents events,
        string targetHost,
        IReadOnlyList<string> offeredApplicationProtocols)
    {
        var (suites, cipherFailure) = SelectCipherSuites();
        if (cipherFailure is not null)
        {
            return (null, ConnectResult.Failed(CurlExitCode.SslCipher, cipherFailure));
        }

        var (profile, listFailure) = CurvesAndSignatureAlgorithms.Apply(Profile, _options);
        if (listFailure is not null)
        {
            return (null, listFailure);
        }

        var (clientCertificate, clientCertificateFailure) = ClientCertificateLoader.Load(_options, _matchesSchannelBuild, _certificateStore, _timeProvider.GetUtcNow());
        if (clientCertificateFailure is not null)
        {
            return (null, clientCertificateFailure);
        }

        events.ReportTlsTrust(SslStreamTlsProvider.DescribeTrust(_options, targetHost));
        try
        {
            var (chainPolicy, anchorsBesideSystemStore, revocationLists) = _verification.ReadTrustAnchors();
            return (new PreparedHandshake(
                ClientSettings.Of(targetHost, offeredApplicationProtocols, suites, ToTlsClientCertificate(clientCertificate), profile!) with
                {
                    RequestOcspStatus = _options.RequireCertificateStatus,
                    TimeProvider = _timeProvider,
                },
                clientCertificate,
                new HandBuiltCertificateVerifier(_verification, chainPolicy, anchorsBesideSystemStore, revocationLists, targetHost)), null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            clientCertificate?.Dispose();
            var (exitCode, message) = _verification.TrustAnchorsUnusable(exception);
            return (null, ConnectResult.Failed(exitCode, message));
        }
    }

    // The handshake's outcome, or what it threw: the transport's failures and cancellation.
    private async Task<(HandBuiltHandshake? Handshake, Exception? Thrown)> TryHandshakeAsync(
        IConnection plaintext,
        PreparedHandshake prepared,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await HandshakeAsync(new ConnectionStream(plaintext), prepared.Settings, prepared.Verifier, cancellationToken).ConfigureAwait(false), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    // A certificate the verifier rejected fails as the SslStream provider fails it, a stapled
    // OCSP response --cert-status rejected is exit 91 (ADR-0191), and any other handshake
    // failure is exit 35.
    private ConnectResult FailedHandshake(TlsHandshakeFailure failure) =>
        failure.CertificateRejection is ValueTuple<CurlExitCode, string> rejected
            ? ConnectResult.Failed(rejected.Item1, rejected.Item2)
            : failure.CertificateStatusRejection is { } statusRejection
            ? ConnectResult.Failed(CurlExitCode.SslInvalidCertStatus, CertificateStatusFailureMessages.For(statusRejection))
            : ConnectResult.Failed(CurlExitCode.SslConnectError, HandshakeFailureMessage(failure));

    private async Task<HandBuiltHandshake> HandshakeAsync(
        Stream transport,
        ClientSettings settings,
        IServerCertificateVerifier verifier,
        CancellationToken cancellationToken)
    {
        // Every TLS 1.3 suite is runnable (BL-811), and a --tls13-ciphers list naming none
        // is exit 59 before this, so a range reaching TLS 1.3 always offers it.
        var runsTls13 = OffersTls13;
        var runsTls12 = OffersBelowTls13 && settings.OffersSuiteFor(IsTls12Suite);
        return runsTls13 && runsTls12 ? await HandshakeTls13OrTls12Async(transport, settings, verifier, cancellationToken).ConfigureAwait(false)
            : runsTls13 ? await HandshakeTls13Async(transport, settings, verifier, cancellationToken).ConfigureAwait(false)
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
        CancellationToken cancellationToken)
    {
        var offer = new TlsClientSettings(settings.ToTls13(alongsideTls12: true), settings.ToTls12(_options));
        return Describe(await TlsClientConnection.ConnectAsync(transport, offer, _random, verifier, cancellationToken).ConfigureAwait(false));
    }

    private async Task<HandBuiltHandshake> HandshakeTls13Async(
        Stream transport,
        ClientSettings settings,
        IServerCertificateVerifier verifier,
        CancellationToken cancellationToken)
    {
        var tls13 = await Tls13ClientConnection.ConnectAsync(transport, settings.ToTls13(alongsideTls12: false), _random, verifier, cancellationToken).ConfigureAwait(false);
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
    // text for --sigalgs, and for --curves the handshake_failure alert as curl.se's LibreSSL
    // build prints it (ADR-0284).
    private string HandshakeFailureMessage(TlsHandshakeFailure failure) =>
        !_matchesSchannelBuild || _options.SignatureAlgorithms is not null ? TlsFailureMessages.OpenSslHandBuiltHandshakeFailure(failure)
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
            CipherSuites = [.. OfferedSuites.Where(Tls13RecordProtection.CanProtect)],
            SupportedGroups = alongsideTls12 ? Profile.SupportedGroups : [.. Profile.SupportedGroups.Where(TlsNamedGroup.CanShare)],
            KeyShareGroups = Profile.KeyShareGroups,
            SignatureAlgorithms = ClientHelloProfileMapping.CheckableSignatureAlgorithms(Profile),
            CertificateCompressionAlgorithms = Profile.CertificateCompressionAlgorithms,
            ExtensionOrder = ClientHelloProfileMapping.ExtensionOrder(Profile, RequestOcspStatus),
            FixedExtensions = ClientHelloProfileMapping.FixedExtensions(Profile),
            SendLegacySessionId = true,
        };

        // Whether the suites to offer include one the predicate accepts; the profiles always do.
        internal bool OffersSuiteFor(Func<ushort, bool> canProtect) => CipherSuites?.Any(canProtect) ?? true;

        // A range that reaches TLS 1.3 offers TLS 1.2 as its ceiling below it, and with no
        // minimum starts at TLS 1.2, curl's default minimum since 8.10.0 (ADR-0205).
        internal Tls12ClientSettings ToTls12(TlsClientOptions options) => new()
        {
            ServerName = ServerName,
            MinimumVersion = Tls12Minimum(options),
            MaximumVersion = Tls12Maximum(options),
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
        };

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
