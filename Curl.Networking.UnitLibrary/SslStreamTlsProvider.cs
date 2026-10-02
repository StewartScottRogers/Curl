using System.Net;
using System.Net.Security;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The production <see cref="ITlsProvider" />: runs the client handshake with .NET's
/// <see cref="SslStream" /> over the plaintext connection, verifying the server
/// certificate and host name as curl does unless <see cref="TlsClientOptions.Insecure" />
/// is set. With <see cref="TlsClientOptions.CaCertificateFile" /> the chain must lead to a
/// certificate in that PEM file instead of the system store. It and
/// <see cref="KerberosKdcProxyTlsClient" />, whose trust is a KDC proxy's own (ADR-0300), are the
/// only types in the solution that construct an <see cref="SslStream" />.
/// </summary>
/// <remarks>
/// Per ADR-0009 it behaves like the curl build the platform usually runs: the Schannel
/// build on Windows and the OpenSSL build elsewhere. The two differ in their failure
/// messages, in what makes a <c>--cacert</c> file exit 77, and in
/// <see cref="TlsClientOptions.CaCertificateDirectory" />, which the OpenSSL build honours
/// and the Schannel build ignores with <see cref="Warnings" />.
/// </remarks>
public sealed class SslStreamTlsProvider : IHandshakeReportingTlsProvider, ITlsProviderWithWarnings
{
    /// <inheritdoc />
    TlsClientRoute IHandshakeReportingTlsProvider.Route => TlsClientRoute.SslStream;

    // The one warning curl 8.21.0's Schannel build writes for --capath (ADR-0009), unwrapped:
    // the console wraps it at the terminal width, into two lines at curl's default 79 columns.
    private static readonly string[] SchannelCaCertificateDirectoryWarnings =
    [
        "Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel",
    ];

    // Held in a field so the delegate is made once, not cached behind a branch in every constructor.
    private static readonly Func<SslStream, SslClientAuthenticationOptions, CancellationToken, Task> SslStreamAuthenticateAsClientAsync =
        static (sslStream, authenticationOptions, cancellationToken) =>
            sslStream.AuthenticateAsClientAsync(authenticationOptions, cancellationToken);

    /// <summary>
    /// The CA bundle curl's OpenSSL build names in its <c>SSL Trust Anchors:</c> lines when
    /// <c>--cacert</c> is not given: <c>/cacert.pem</c>, as the reference build,
    /// <c>curlimages/curl:8.21.0</c>, prints it (measured, BL-405). It is reported, never read.
    /// </summary>
    internal const string OpenSslDefaultCaCertificateFile = "/cacert.pem";

    private readonly TlsClientOptions _options;

    private readonly bool _matchesSchannelBuild;

    private readonly SslProtocols _offeredProtocols;

    private readonly TimeProvider _timeProvider;

    private readonly IClientCertificateStore _certificateStore;

    private readonly ServerCertificateVerification _verification;

    /// <summary>
    /// Creates the provider for the curl build this platform usually runs: Schannel on
    /// Windows, OpenSSL elsewhere, timing its handshakes on <see cref="TimeProvider.System" />.
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    public SslStreamTlsProvider(TlsClientOptions options)
        : this(options, TimeProvider.System)
    {
    }

    /// <summary>
    /// Creates the provider for the curl build this platform usually runs: Schannel on
    /// Windows, OpenSSL elsewhere.
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    /// <param name="timeProvider">
    /// Takes the timestamps in a successful handshake's <see cref="ConnectResult.Timings" />;
    /// pass the connector's, so a reader can subtract one connector timestamp from another.
    /// </param>
    public SslStreamTlsProvider(TlsClientOptions options, TimeProvider timeProvider)
        : this(options, OperatingSystem.IsWindows(), timeProvider)
    {
    }

    /// <summary>
    /// Creates the provider for a named curl build, so either build's behaviour can be
    /// tested on any platform, timing its handshakes on <see cref="TimeProvider.System" />.
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    /// <param name="matchesSchannelBuild">
    /// <see langword="true" /> to behave like curl's Schannel build, <see langword="false" />
    /// like its OpenSSL build.
    /// </param>
    internal SslStreamTlsProvider(TlsClientOptions options, bool matchesSchannelBuild)
        : this(options, matchesSchannelBuild, TimeProvider.System)
    {
    }

    /// <summary>
    /// Creates the provider for a named curl build with the clock its handshakes are timed on.
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    /// <param name="matchesSchannelBuild">
    /// <see langword="true" /> to behave like curl's Schannel build, <see langword="false" />
    /// like its OpenSSL build.
    /// </param>
    /// <param name="timeProvider">Takes the timestamps in a successful handshake's timings.</param>
    internal SslStreamTlsProvider(TlsClientOptions options, bool matchesSchannelBuild, TimeProvider timeProvider)
        : this(options, matchesSchannelBuild, timeProvider, new SystemClientCertificateStore())
    {
    }

    /// <summary>
    /// Creates the provider for a named curl build with the clock its handshakes are timed on
    /// and the certificate stores a Schannel <c>--cert</c> store path is looked up in.
    /// </summary>
    /// <param name="options">The settings applied to every handshake.</param>
    /// <param name="matchesSchannelBuild">
    /// <see langword="true" /> to behave like curl's Schannel build, <see langword="false" />
    /// like its OpenSSL build.
    /// </param>
    /// <param name="timeProvider">Takes the timestamps in a successful handshake's timings.</param>
    /// <param name="certificateStore">Opens the store a Schannel <c>--cert</c> store path names.</param>
    /// <exception cref="ArgumentException">
    /// <see cref="TlsClientOptions.MinimumVersion" /> is above <see cref="TlsClientOptions.MaximumVersion" />.
    /// </exception>
    internal SslStreamTlsProvider(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        TimeProvider timeProvider,
        IClientCertificateStore certificateStore)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _offeredProtocols = TlsVersionRange.ToSslProtocols(options.MinimumVersion, options.MaximumVersion);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        _matchesSchannelBuild = matchesSchannelBuild;
        _verification = new ServerCertificateVerification(options, matchesSchannelBuild, timeProvider);
        Warnings = WarningsFor(options, matchesSchannelBuild);
    }

    /// <summary>
    /// Returns the lines the build writes to standard error for options it ignores, as
    /// <see cref="Warnings" /> holds them: the Schannel build's one <c>--capath</c> warning,
    /// unwrapped, when <see cref="TlsClientOptions.CaCertificateDirectory" /> is set, otherwise
    /// none. <see cref="HandBuiltTlsProvider" /> reports the same.
    /// </summary>
    /// <param name="options">The settings the handshakes run with.</param>
    /// <param name="matchesSchannelBuild">Whether the provider behaves like curl's Schannel build.</param>
    /// <returns>The warning lines, each without its line ending.</returns>
    internal static IReadOnlyList<string> WarningsFor(TlsClientOptions options, bool matchesSchannelBuild) =>
        matchesSchannelBuild && options.CaCertificateDirectory is not null
            ? SchannelCaCertificateDirectoryWarnings
            : [];

    /// <summary>
    /// Gets the lines curl writes to standard error, unless <c>-s</c> is given, for options
    /// this build ignores: the Schannel build's one <c>--capath</c> warning, unwrapped, when
    /// <see cref="TlsClientOptions.CaCertificateDirectory" /> is set, otherwise none. Each
    /// line is without its line ending.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Gets the factory that builds the OpenSSL build's <see cref="CipherSuitesPolicy" /> from
    /// the suites <c>--ciphers</c> and <c>--tls13-ciphers</c> select, returning
    /// <see langword="null" /> where the platform cannot apply them. Defaults to
    /// <see cref="Networking.CipherSuitesPolicyFactory.ForThisPlatform" />; tests replace it.
    /// </summary>
    internal ICipherSuitesPolicyFactory CipherSuitesPolicyFactory { get; init; } =
        Networking.CipherSuitesPolicyFactory.ForThisPlatform;

    /// <summary>
    /// Gets the step that runs the client handshake on the <see cref="SslStream" /> with the
    /// options the provider built. Defaults to
    /// <see cref="SslStream.AuthenticateAsClientAsync(SslClientAuthenticationOptions, CancellationToken)" />;
    /// tests replace it to see the options.
    /// </summary>
    internal Func<SslStream, SslClientAuthenticationOptions, CancellationToken, Task> AuthenticateSslStreamAsClientAsync { get; init; } =
        SslStreamAuthenticateAsClientAsync;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The target host is passed to the handshake for server name indication and is the
    /// name the certificate is checked against. A certificate that fails the check is
    /// exit 60 (<see cref="CurlExitCode.PeerFailedVerification" />), except that the
    /// Schannel build reports a certificate that is only out of date, checked against the
    /// system store, as exit 35 with Schannel's <c>SEC_E_CERT_EXPIRED</c>. With
    /// <see cref="TlsClientOptions.CaCertificateFile" /> the Schannel build also checks
    /// revocation, unless <see cref="TlsClientOptions.SkipRevocationCheck" /> is set, and an
    /// unknown revocation status is exit 60 (ADR-0321). Any other failure,
    /// such as no TLS version both sides allow or the server closing mid-handshake, is
    /// exit 35 (<see cref="CurlExitCode.SslConnectError" />). The handshake offers the
    /// versions from <see cref="TlsClientOptions.MinimumVersion" /> up to
    /// <see cref="TlsClientOptions.MaximumVersion" /> (<see cref="TlsVersionRange" />). Where
    /// the operating system will not offer a range below TLS 1.2 at all, the Schannel build
    /// reports what curl's Schannel build reports when the server refuses it,
    /// <c>failed to receive handshake</c> (measured, BL-502); connecting to a server that
    /// speaks only TLS 1.0 or 1.1 where the operating system refuses them is BL-714's
    /// hand-built TLS client. A
    /// <see cref="TlsClientOptions.CaCertificateFile" /> that cannot be read is exit 77
    /// (<see cref="CurlExitCode.SslCacertBadfile" />). In the OpenSSL build so is one that
    /// holds no certificate or a certificate block that does not parse; in the Schannel
    /// build such a file trusts only the certificates that do parse, so verification fails
    /// with exit 60 when there are none. In the OpenSSL build every certificate in the
    /// <see cref="TlsClientOptions.CaCertificateDirectory" /> is trusted beside the
    /// <c>--cacert</c> file or the system store; a missing directory, or a file in it that
    /// cannot be read, adds nothing. No file is read when
    /// <see cref="TlsClientOptions.Insecure" /> is set. A <see cref="TlsClientOptions.Ciphers" />
    /// or <see cref="TlsClientOptions.Tls13Ciphers" /> value the build cannot apply is exit 59
    /// (<see cref="CurlExitCode.SslCipher" />), as ADR-0011 decides. The plaintext connection is
    /// disposed on every failure and on cancellation.
    /// </para>
    /// <para>
    /// A success carries <see cref="ConnectResult.Timings" /> from the provider's
    /// <see cref="TimeProvider" />: <see cref="ConnectTimings.TlsHandshakeCompleted" /> when the
    /// handshake completed, and <see cref="ConnectTimings.Started" /> and
    /// <see cref="ConnectTimings.Connected" /> both when it began, the moment the plaintext
    /// connection was handed over; <see cref="ConnectTimings.NameResolved" /> is
    /// <see langword="null" />, since the provider resolves nothing. <see cref="TcpConnector" />
    /// keeps only the handshake's completion and supplies the rest itself.
    /// </para>
    /// <para>
    /// A success also carries <see cref="ConnectResult.PeerCertificates" />: the server's
    /// certificate and then the others it sent, in the order sent, as curl's Schannel build
    /// lists them for <c>%{certs}</c> (ADR-0054). They are taken in the validation callback,
    /// whether or not the certificate is verified, since <see cref="SslStream" /> hands the
    /// rest of what the server sent to that callback alone.
    /// </para>
    /// </remarks>
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, NoTransferEvents.Instance, cancellationToken);

    /// <summary>
    /// Performs the client handshake as
    /// <see cref="AuthenticateAsClientAsync(IConnection, string, CancellationToken)" /> does
    /// and, when it succeeds, reports a <see cref="TlsHandshakeEvent" /> to
    /// <paramref name="events" /> (BL-404).
    /// </summary>
    /// <remarks>
    /// The event carries the negotiated version and cipher suite, the server's certificate,
    /// whether its chain verified, <see cref="TlsHandshakeEvent.CertificateVerifyResult" /> as
    /// <see cref="OpenSslVerifyResult" /> maps it, and
    /// <see cref="TlsHandshakeEvent.PeerCertificateChain" />: the verified chain when the chain
    /// verified, else what the server sent. This overload offers no ALPN, and
    /// <see cref="SslStream" /> exposes neither the key-exchange group nor the peer's signature
    /// type, so those stay <see langword="null" /> (ADR-0085).
    /// </remarks>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the completed handshake is reported.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result the three-argument overload describes.</returns>
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, events, isProxy: false, cancellationToken);

    /// <summary>
    /// Performs the client handshake as
    /// <see cref="AuthenticateAsClientAsync(IConnection, string, CancellationToken)" /> does,
    /// reporting to <paramref name="events" /> a <see cref="TlsTrustEvent" /> before the
    /// handshake and, when it succeeds, a <see cref="TlsHandshakeEvent" /> (BL-404, BL-452).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The trust is reported once the cipher suites and any client certificate are ready and
    /// before a <see cref="TlsClientOptions.CaCertificateFile" /> is read, as curl's OpenSSL
    /// build writes its <c>SSL Trust</c> lines before it loads the file:
    /// <see cref="TlsTrustEvent.VerifiesPeer" /> unless <see cref="TlsClientOptions.Insecure" />,
    /// the <c>--cacert</c> file or else <see cref="OpenSslDefaultCaCertificateFile" />, and the
    /// <c>--capath</c> directory. The default file is only named: verification without
    /// <c>--cacert</c> still uses the system store.
    /// </para>
    /// <para>
    /// The handshake event is as the four-argument overload describes, with
    /// <see cref="TlsHandshakeEvent.IsProxy" /> from <paramref name="isProxy" /> and
    /// <see cref="TlsHandshakeEvent.VerifiedHostName" /> the target host without IPv6
    /// brackets, or <see langword="null" /> under <see cref="TlsClientOptions.Insecure" />.
    /// <see cref="SslStream" /> exposes no TLS records, so no <see cref="TlsMessageEvent" /> is
    /// reported (ADR-0085), except in the Schannel build after a TLS 1.3 handshake: each session
    /// ticket record <see cref="SessionTicketRecordDetector" /> finds before the first
    /// application data is reported, from the connection's first read, as a received
    /// <c>NewSessionTicket</c> (ADR-0309, BL-1089).
    /// </para>
    /// </remarks>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the trust and the completed handshake are reported.</param>
    /// <param name="isProxy">
    /// <see langword="true" /> when the handshake is with an HTTPS proxy rather than the origin.
    /// </param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result the three-argument overload describes.</returns>
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        bool isProxy,
        CancellationToken cancellationToken) =>
        AuthenticateAsClientAsync(plaintext, targetHost, events, isProxy, [], cancellationToken);

    /// <summary>
    /// Performs the client handshake as
    /// <see cref="AuthenticateAsClientAsync(IConnection, string, ITransferEvents, bool, CancellationToken)" />
    /// does, offering <paramref name="applicationProtocols" /> through ALPN unless
    /// <see cref="TlsClientOptions.UseAlpn" /> is off (<c>--no-alpn</c>), in which case the
    /// handshake carries no ALPN extension (BL-490).
    /// </summary>
    /// <remarks>
    /// The <see cref="TlsHandshakeEvent" /> names what was offered and what the server
    /// selected, <see langword="null" /> when it selected nothing, so <c>-v</c> prints curl's
    /// <c>ALPN:</c> lines, and none when nothing was offered.
    /// </remarks>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the trust and the completed handshake are reported.</param>
    /// <param name="isProxy">
    /// <see langword="true" /> when the handshake is with an HTTPS proxy rather than the origin.
    /// </param>
    /// <param name="applicationProtocols">The protocols to offer through ALPN, in preference order.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result the three-argument overload describes.</returns>
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

        var offeredApplicationProtocols = OfferedApplicationProtocols(applicationProtocols);
        var (cipherSuitesPolicy, cipherFailure) = CreateCipherSuitesPolicy();
        if (cipherFailure is not null)
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            return ConnectResult.Failed(CurlExitCode.SslCipher, cipherFailure);
        }

        var (clientCertificate, clientCertificateFailure) = LoadClientCertificate();
        if (clientCertificateFailure is not null)
        {
            ReportTrustBeforeClientCertificateFailure(events, _options, targetHost, _matchesSchannelBuild);
            await plaintext.DisposeAsync().ConfigureAwait(false);
            return clientCertificateFailure;
        }

        events.ReportTlsTrust(DescribeTrust(_options, targetHost));
        X509ChainPolicy? chainPolicy;
        X509Certificate2Collection anchorsBesideSystemStore;
        CertificateRevocationListFile? revocationLists;
        try
        {
            (chainPolicy, anchorsBesideSystemStore, revocationLists) = _verification.ReadTrustAnchors();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            clientCertificate?.Dispose();
            await plaintext.DisposeAsync().ConfigureAwait(false);
            var (exitCode, message) = _verification.TrustAnchorsUnusable(exception);
            return ConnectResult.Failed(exitCode, message);
        }

        (CurlExitCode ExitCode, string Message)? verificationFailure = null;
        ReadOnlyMemory<byte>[] peerCertificates = [];
        var peerVerification = PeerVerification.Unobserved;
        var authenticationOptions = new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            EnabledSslProtocols = _offeredProtocols,
            CertificateChainPolicy = chainPolicy,
            LocalCertificateSelectionCallback = ToCertificateSelection(clientCertificate),
            CipherSuitesPolicy = cipherSuitesPolicy,
            ApplicationProtocols = ToSslApplicationProtocols(offeredApplicationProtocols),
            RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
            {
                peerCertificates = ListPeerCertificates(certificate, chain);
                (peerVerification, verificationFailure) = _verification.Judge(
                    errors, chain, targetHost, anchorsBesideSystemStore, revocationLists, peerCertificates);
                return verificationFailure is null;
            },
        };

        var transport = OpenTransport(plaintext);
        var sslStream = new SslStream(transport, leaveInnerStreamOpen: true);
        Exception failure;
        try
        {
            var handshakeStarted = _timeProvider.GetTimestamp();
            await AuthenticateSslStreamAsClientAsync(sslStream, authenticationOptions, cancellationToken).ConfigureAwait(false);
            peerVerification.ReportVerifyResult(events, isProxy, _matchesSchannelBuild);
            events.ReportTlsHandshake(DescribeHandshake(sslStream, peerVerification, offeredApplicationProtocols) with
            {
                IsProxy = isProxy,
                VerifiedHostName = VerifiedHostName(targetHost, _options.Insecure),
            });
            return ConnectResult.Connected(
                new SslStreamConnection(sslStream, transport, plaintext, clientCertificate, TlsFailureMessages.MissingCloseNotify(_matchesSchannelBuild), clearsTls: !_matchesSchannelBuild)
                {
                    TicketRecords = FollowTicketRecordsAfterHandshake(transport, sslStream.SslProtocol),
                    TicketEvents = events,
                },
                new ConnectTimings(handshakeStarted, null, handshakeStarted, _timeProvider.GetTimestamp()),
                peerCertificates: peerCertificates,
                applicationProtocol: NegotiatedApplicationProtocol(sslStream.NegotiatedApplicationProtocol));
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        clientCertificate?.Dispose();
        await DisposeAfterFailedHandshakeAsync(sslStream, plaintext).ConfigureAwait(false);
        RethrowIfCancellation(failure);
        peerVerification.ReportVerifyResult(events, isProxy, _matchesSchannelBuild);

        return verificationFailure is { } rejected
            ? ConnectResult.Failed(rejected.ExitCode, rejected.Message)
            : ConnectResult.Failed(CurlExitCode.SslConnectError, SslConnectError(failure));
    }

    /// <summary>
    /// Decides whether the server certificate is accepted, and if not, what curl says.
    /// </summary>
    /// <param name="errors">What <see cref="SslStream" /> found wrong.</param>
    /// <param name="chain">The chain it built, if a certificate was presented.</param>
    /// <param name="targetHost">The host the certificate was checked against.</param>
    /// <param name="anchorsBesideSystemStore">
    /// Roots trusted in addition to the system store, from <c>--capath</c> without
    /// <c>--cacert</c>.
    /// </param>
    /// <returns>
    /// <see langword="null" /> to accept the certificate, otherwise the exit code and message:
    /// exit 60, except in the Schannel build without <c>--cacert</c>, where a certificate
    /// that is only out of date fails the handshake itself, exit 35.
    /// </returns>
    internal (CurlExitCode ExitCode, string Message)? VerifyPeer(
        SslPolicyErrors errors,
        X509Chain? chain,
        string targetHost,
        X509Certificate2Collection anchorsBesideSystemStore) =>
        _verification.VerifyPeer(errors, chain, targetHost, anchorsBesideSystemStore);

    /// <summary>
    /// Lists what the server sent, as curl's Schannel build does: the server's own
    /// certificate first, then the chain's extra store, which is where
    /// <see cref="SslStream" /> puts the other certificates the server sent, in the order
    /// sent. A copy of the server's own certificate there is not listed twice.
    /// </summary>
    /// <param name="certificate">The server's certificate, <see langword="null" /> when it sent none.</param>
    /// <param name="chain">The chain built for it, <see langword="null" /> when there is none.</param>
    /// <returns>The DER encodings; empty when the server sent no certificate.</returns>
    internal static ReadOnlyMemory<byte>[] ListPeerCertificates(X509Certificate? certificate, X509Chain? chain)
    {
        if (certificate is null)
        {
            return [];
        }

        var serverCertificate = certificate.GetRawCertData();
        var sent = new List<ReadOnlyMemory<byte>> { serverCertificate };
        foreach (var other in chain?.ChainPolicy.ExtraStore ?? [])
        {
            if (!other.RawData.AsSpan().SequenceEqual(serverCertificate))
            {
                sent.Add(other.RawData);
            }
        }

        return [.. sent];
    }

    // The Schannel build watches the records read for TLS 1.3 session tickets (BL-1089).
    private ConnectionStream OpenTransport(IConnection plaintext) =>
        new(plaintext) { TicketRecords = _matchesSchannelBuild ? new SessionTicketRecordDetector() : null };

    /// <summary>
    /// Keeps the transport's <see cref="SessionTicketRecordDetector" /> watching after a TLS 1.3
    /// handshake, the only version whose tickets curl's Schannel build reports (measured with
    /// <c>--tls-max 1.2</c>, BL-1089), and drops it otherwise.
    /// </summary>
    /// <param name="transport">The stream the handshake ran over.</param>
    /// <param name="negotiated">The version the handshake negotiated.</param>
    /// <returns>The detector still watching, or <see langword="null" /> when none is.</returns>
    internal static SessionTicketRecordDetector? FollowTicketRecordsAfterHandshake(ConnectionStream transport, SslProtocols negotiated)
    {
        if (negotiated != SslProtocols.Tls13)
        {
            transport.TicketRecords = null;
        }

        transport.TicketRecords?.MarkHandshakeComplete();
        return transport.TicketRecords;
    }

    private static TlsHandshakeEvent DescribeHandshake(
        SslStream sslStream,
        PeerVerification peerVerification,
        IReadOnlyList<string> offeredApplicationProtocols) => new()
        {
            ProtocolVersion = sslStream.SslProtocol,
            CipherSuite = sslStream.NegotiatedCipherSuite,
            NegotiatedApplicationProtocol = NegotiatedApplicationProtocol(sslStream.NegotiatedApplicationProtocol),
            OfferedApplicationProtocols = offeredApplicationProtocols,
            ServerCertificate = sslStream.RemoteCertificate as X509Certificate2,
            CertificateVerified = peerVerification.Verified,
            CertificateVerifyResult = peerVerification.VerifyResult,
            PeerCertificateChain = [.. peerVerification.Chain.Select(der => X509CertificateLoader.LoadCertificate(der.Span))],
        };

    /// <summary>
    /// Names the protocol the server selected through ALPN, as curl's <c>ALPN: server accepted</c>
    /// line names it.
    /// </summary>
    /// <param name="negotiated">What <see cref="SslStream.NegotiatedApplicationProtocol" /> returned.</param>
    /// <returns>The protocol's name, or <see langword="null" /> when the server selected none.</returns>
    internal static string? NegotiatedApplicationProtocol(SslApplicationProtocol negotiated) =>
        negotiated.Protocol.IsEmpty ? null : negotiated.ToString();

    /// <summary>
    /// Turns the protocols to offer through ALPN into the list
    /// <see cref="SslClientAuthenticationOptions.ApplicationProtocols" /> takes.
    /// </summary>
    /// <param name="applicationProtocols">The protocols, in preference order.</param>
    /// <returns>
    /// <see langword="null" />, which sends no ALPN extension, when there are none; otherwise
    /// the protocols in the same order.
    /// </returns>
    internal static List<SslApplicationProtocol>? ToSslApplicationProtocols(IReadOnlyList<string> applicationProtocols) =>
        applicationProtocols.Count == 0
            ? null
            : [.. applicationProtocols.Select(protocol => new SslApplicationProtocol(protocol))];

    // What the handshake offers through ALPN: the connection's protocols, or none under --no-alpn.
    private IReadOnlyList<string> OfferedApplicationProtocols(IReadOnlyList<string> applicationProtocols)
    {
        ArgumentNullException.ThrowIfNull(applicationProtocols);

        return _options.UseAlpn ? applicationProtocols : [];
    }

    /// <summary>
    /// Describes the trust a handshake with <paramref name="options" /> verifies against, as
    /// both TLS clients report it before the handshake: <c>-k</c>, the <c>--cacert</c> file or
    /// else <see cref="OpenSslDefaultCaCertificateFile" />, the <c>--capath</c> directory,
    /// <c>--ssl-auto-client-cert</c>, and whether <paramref name="targetHost" /> is an IP address.
    /// </summary>
    /// <param name="options">The handshake's settings.</param>
    /// <param name="targetHost">The host the handshake connects to; an IPv6 literal may keep its brackets.</param>
    /// <returns>The event.</returns>
    internal static TlsTrustEvent DescribeTrust(TlsClientOptions options, string targetHost) => new()
    {
        UsesAutomaticClientCertificate = options.AutoClientCertificate,
        TargetsIpAddress = IPAddress.TryParse(targetHost.Trim('[', ']'), out _),
        VerifiesPeer = !options.Insecure,
        CaCertificateFile = options.CaCertificateFile ?? OpenSslDefaultCaCertificateFile,
        CaCertificateDirectory = options.CaCertificateDirectory,
    };

    /// <summary>
    /// Reports, in the Schannel build only, the trust curl's Schannel build writes before a
    /// <c>--cert</c> that does not load: its <c>schannel_acquire_credential_handle</c> writes
    /// whether the client certificate is picked automatically, then fails on the certificate,
    /// before <c>schannel_connect_step1</c> would write the SNI line, so the event carries
    /// <see cref="TlsTrustEvent.TargetsIpAddress" /> <see langword="false" /> whatever the host
    /// (measured with curl 8.21.0, BL-1088, ADR-0305). The OpenSSL build reports nothing.
    /// </summary>
    /// <param name="events">Where the trust is reported.</param>
    /// <param name="options">The handshake's settings.</param>
    /// <param name="targetHost">The host the handshake connects to.</param>
    /// <param name="matchesSchannelBuild">Whether the Schannel build is reproduced.</param>
    internal static void ReportTrustBeforeClientCertificateFailure(ITransferEvents events, TlsClientOptions options, string targetHost, bool matchesSchannelBuild)
    {
        if (matchesSchannelBuild)
        {
            events.ReportTlsTrust(DescribeTrust(options, targetHost) with { TargetsIpAddress = false });
        }
    }

    /// <summary>
    /// Returns the host name a handshake checks the certificate against, as
    /// <see cref="TlsHandshakeEvent.VerifiedHostName" /> carries it: the target host, an IPv6
    /// literal without its brackets, or <see langword="null" /> under <c>-k</c>, which checks
    /// no host name.
    /// </summary>
    /// <param name="targetHost">The host the handshake was asked to verify.</param>
    /// <param name="insecure">Whether <c>-k</c> (or <c>--proxy-insecure</c>) is set.</param>
    /// <returns>The host name, or <see langword="null" />.</returns>
    internal static string? VerifiedHostName(string targetHost, bool insecure) =>
        insecure ? null
        : targetHost.StartsWith('[') && targetHost.EndsWith(']') ? targetHost[1..^1]
        : targetHost;

    // ADR-0011: the Schannel build refuses --ciphers and ignores --tls13-ciphers; the
    // OpenSSL build offers what they name. CipherSuitesPolicy cannot be constructed on
    // Windows, where only the tests run the OpenSSL build, so there the factory builds none
    // and the build cannot apply them.
    private (CipherSuitesPolicy? Policy, string? FailureMessage) CreateCipherSuitesPolicy()
    {
        if (_matchesSchannelBuild)
        {
            return (null, _options.Ciphers is null ? null : TlsFailureMessages.SchannelCipherListRefused);
        }

        var (suites, failureMessage) = OpenSslCipherSuites.Select(_options.Ciphers, _options.Tls13Ciphers);
        if (suites is null)
        {
            return (null, failureMessage);
        }

        var policy = CipherSuitesPolicyFactory.Create(suites);
        return policy is null
            ? (null, OpenSslCipherSuites.Unapplied(_options.Ciphers, _options.Tls13Ciphers))
            : (policy, null);
    }

    private (X509Certificate2? Certificate, ConnectResult? Failure) LoadClientCertificate() =>
        ClientCertificateLoader.Load(_options, _matchesSchannelBuild, _certificateStore, _timeProvider.GetUtcNow());

    // Selected by callback, not given as ClientCertificateContext: on Windows SslStream opens
    // a handshake that has a certificate context with a credential handle that carries it,
    // then caches that handle as the one for "no certificate", so every later handshake
    // without --cert in the process presented it (BL-254). A callback leaves that first
    // handle anonymous, and the certificate is sent only when the server asks for one,
    // whatever issuers it names, as curl sends --cert.
    private static LocalCertificateSelectionCallback? ToCertificateSelection(X509Certificate2? clientCertificate) =>
        clientCertificate is null ? null : (_, _, _, _, _) => clientCertificate;

    private string SslConnectError(Exception failure) => _matchesSchannelBuild
        ? TlsFailureMessages.SchannelSslConnectError(failure, OffersOnlyVersionsBelowTls12)
        : TlsFailureMessages.OpenSslSslConnectError(failure);

    private bool OffersOnlyVersionsBelowTls12 => _options.MaximumVersion is TlsVersion.Tls10 or TlsVersion.Tls11;

    // Cancellation is the one exception ITlsProvider lets escape; its stack trace is kept.
    private static void RethrowIfCancellation(Exception failure)
    {
        if (failure is not OperationCanceledException)
        {
            return;
        }

        ExceptionDispatchInfo.Throw(failure);
    }

    // Disposing an SslStream flushes its inner stream, which throws again when the
    // plaintext connection is what failed; the handshake's own failure is the one to report.
    private static async ValueTask DisposeAfterFailedHandshakeAsync(SslStream sslStream, IConnection plaintext)
    {
        try
        {
            await sslStream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Already failed; closing it cannot fail it further.
        }

        await plaintext.DisposeAsync().ConfigureAwait(false);
    }
}
