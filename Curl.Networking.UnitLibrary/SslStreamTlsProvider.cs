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
/// certificate in that PEM file instead of the system store. It is the only type in the
/// solution that constructs an <see cref="SslStream" />.
/// </summary>
/// <remarks>
/// Per ADR-0009 it behaves like the curl build the platform usually runs: the Schannel
/// build on Windows and the OpenSSL build elsewhere. The two differ in their failure
/// messages, in what makes a <c>--cacert</c> file exit 77, and in
/// <see cref="TlsClientOptions.CaCertificateDirectory" />, which the OpenSSL build honours
/// and the Schannel build ignores with <see cref="Warnings" />.
/// </remarks>
public sealed class SslStreamTlsProvider : IHandshakeReportingTlsProvider
{
    private const string PemCertificateBegin = "-----BEGIN CERTIFICATE-----";

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

    private readonly TlsClientOptions _options;

    private readonly bool _matchesSchannelBuild;

    private readonly TimeProvider _timeProvider;

    private readonly IClientCertificateStore _certificateStore;

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
    internal SslStreamTlsProvider(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        TimeProvider timeProvider,
        IClientCertificateStore certificateStore)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        _matchesSchannelBuild = matchesSchannelBuild;
        Warnings = matchesSchannelBuild && options.CaCertificateDirectory is not null
            ? SchannelCaCertificateDirectoryWarnings
            : [];
    }

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
    /// unknown revocation status is exit 60 (ADR-0086). Any other failure,
    /// such as no TLS version both sides allow or the server closing mid-handshake, is
    /// exit 35 (<see cref="CurlExitCode.SslConnectError" />). A
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
    /// verified, else what the server sent. No ALPN is offered, and
    /// <see cref="SslStream" /> exposes neither the key-exchange group nor the peer's signature
    /// type, so those stay <see langword="null" /> (ADR-0085).
    /// </remarks>
    /// <param name="plaintext">The connection to upgrade; ownership transfers to the provider.</param>
    /// <param name="targetHost">The host name to validate the server certificate against.</param>
    /// <param name="events">Where the completed handshake is reported.</param>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <returns>The same result the three-argument overload describes.</returns>
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetHost);

        var (cipherSuitesPolicy, cipherFailure) = CreateCipherSuitesPolicy();
        if (cipherFailure is not null)
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            return ConnectResult.Failed(CurlExitCode.SslCipher, cipherFailure);
        }

        var (clientCertificate, clientCertificateFailure) = LoadClientCertificate();
        if (clientCertificateFailure is not null)
        {
            await plaintext.DisposeAsync().ConfigureAwait(false);
            return clientCertificateFailure;
        }

        X509ChainPolicy? chainPolicy;
        X509Certificate2Collection anchorsBesideSystemStore;
        try
        {
            (chainPolicy, anchorsBesideSystemStore) = ReadTrustAnchors();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            clientCertificate?.Dispose();
            await plaintext.DisposeAsync().ConfigureAwait(false);
            return ConnectResult.Failed(CurlExitCode.SslCacertBadfile, CaCertificateFileUnusable(_options.CaCertificateFile!));
        }

        (CurlExitCode ExitCode, string Message)? verificationFailure = null;
        ReadOnlyMemory<byte>[] peerCertificates = [];
        var peerVerification = PeerVerification.Unobserved;
        var authenticationOptions = new SslClientAuthenticationOptions
        {
            TargetHost = targetHost,
            EnabledSslProtocols = ToSslProtocols(_options.MinimumVersion),
            CertificateChainPolicy = chainPolicy,
            LocalCertificateSelectionCallback = ToCertificateSelection(clientCertificate),
            CipherSuitesPolicy = cipherSuitesPolicy,
            RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
            {
                peerCertificates = ListPeerCertificates(certificate, chain);
                var anchoredErrors = WithoutChainErrorsWhenAnchored(errors, chain, anchorsBesideSystemStore);
                peerVerification = ObservePeerVerification(anchoredErrors, chain, peerCertificates);
                verificationFailure = VerifyPeer(anchoredErrors, chain, targetHost, []);
                return verificationFailure is null;
            },
        };

        var sslStream = new SslStream(new ConnectionStream(plaintext), leaveInnerStreamOpen: true);
        Exception failure;
        try
        {
            var handshakeStarted = _timeProvider.GetTimestamp();
            await AuthenticateSslStreamAsClientAsync(sslStream, authenticationOptions, cancellationToken).ConfigureAwait(false);
            events.ReportTlsHandshake(DescribeHandshake(sslStream, peerVerification));
            return ConnectResult.Connected(
                new SslStreamConnection(sslStream, plaintext, clientCertificate),
                new ConnectTimings(handshakeStarted, null, handshakeStarted, _timeProvider.GetTimestamp()),
                peerCertificates: peerCertificates);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        clientCertificate?.Dispose();
        await DisposeAfterFailedHandshakeAsync(sslStream, plaintext).ConfigureAwait(false);
        RethrowIfCancellation(failure);

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
        X509Certificate2Collection anchorsBesideSystemStore)
    {
        if (_options.Insecure)
        {
            return null;
        }

        errors = WithoutChainErrorsWhenAnchored(errors, chain, anchorsBesideSystemStore);
        if (errors == SslPolicyErrors.None)
        {
            return null;
        }

        if (!_matchesSchannelBuild)
        {
            return (CurlExitCode.PeerFailedVerification, TlsFailureMessages.OpenSslPeerFailedVerification(errors, chain, targetHost));
        }

        var hasCaCertificateFile = _options.CaCertificateFile is not null;
        return !hasCaCertificateFile && TlsFailureMessages.IsSchannelCertificateExpired(errors, chain)
            ? (CurlExitCode.SslConnectError, TlsFailureMessages.SchannelCertificateExpired)
            : (CurlExitCode.PeerFailedVerification,
                TlsFailureMessages.SchannelPeerFailedVerification(errors, chain, targetHost, hasCaCertificateFile));
    }

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

    // The chain's errors do not count when it leads to a --capath root trusted beside the
    // system store.
    private static SslPolicyErrors WithoutChainErrorsWhenAnchored(
        SslPolicyErrors errors,
        X509Chain? chain,
        X509Certificate2Collection anchorsBesideSystemStore) =>
        ChainLeadsToAnyOf(chain, anchorsBesideSystemStore)
            ? errors & ~SslPolicyErrors.RemoteCertificateChainErrors
            : errors;

    // Taken in the validation callback, whether or not -k lets the handshake go on, because
    // SslStream disposes the chain once the callback returns.
    private PeerVerification ObservePeerVerification(
        SslPolicyErrors anchoredErrors,
        X509Chain? chain,
        ReadOnlyMemory<byte>[] peerCertificates)
    {
        var reportedChain = chain is { ChainElements.Count: > 0 }
            && (anchoredErrors & SslPolicyErrors.RemoteCertificateChainErrors) == 0
                ? ListChainElements(chain)
                : peerCertificates;
        return new PeerVerification(
            anchoredErrors == SslPolicyErrors.None,
            OpenSslVerifyResult.Of(anchoredErrors, chain, _timeProvider.GetUtcNow()),
            reportedChain);
    }

    private static ReadOnlyMemory<byte>[] ListChainElements(X509Chain chain)
    {
        var elements = new List<ReadOnlyMemory<byte>>();
        foreach (var element in chain.ChainElements)
        {
            elements.Add(element.Certificate.RawData);
        }

        return [.. elements];
    }

    private static TlsHandshakeEvent DescribeHandshake(SslStream sslStream, PeerVerification peerVerification) => new()
    {
        ProtocolVersion = sslStream.SslProtocol,
        CipherSuite = sslStream.NegotiatedCipherSuite,
        NegotiatedApplicationProtocol = null,
        OfferedApplicationProtocols = [],
        ServerCertificate = sslStream.RemoteCertificate as X509Certificate2,
        CertificateVerified = peerVerification.Verified,
        CertificateVerifyResult = peerVerification.VerifyResult,
        PeerCertificateChain = [.. peerVerification.Chain.Select(der => X509CertificateLoader.LoadCertificate(der.Span))],
    };

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

    // The Schannel build reads the certificate and its key from a Windows certificate store
    // or a PKCS#12 file and ignores --key and --key-type; only the Windows build of curl
    // keeps a drive letter's colon in the --cert value. --pass, when given, is the
    // passphrase in place of the one in --cert.
    private (X509Certificate2? Certificate, ConnectResult? Failure) LoadClientCertificate()
    {
        if (_options.ClientCertificate is null)
        {
            return (null, null);
        }

        var (path, splitPassphrase) = ClientCertificateArgument.Split(_options.ClientCertificate, _matchesSchannelBuild);
        var passphrase = _options.Passphrase ?? splitPassphrase;
        return _matchesSchannelBuild
            ? ClientCertificateLoader.LoadAsSchannelBuild(path, passphrase, _options.CertificateType, _certificateStore)
            : ClientCertificateLoader.LoadAsOpenSslBuild(
                path,
                passphrase,
                _options.PrivateKey,
                _options.CertificateType,
                _options.PrivateKeyType);
    }

    // Selected by callback, not given as ClientCertificateContext: on Windows SslStream opens
    // a handshake that has a certificate context with a credential handle that carries it,
    // then caches that handle as the one for "no certificate", so every later handshake
    // without --cert in the process presented it (BL-254). A callback leaves that first
    // handle anonymous, and the certificate is sent only when the server asks for one,
    // whatever issuers it names, as curl sends --cert.
    private static LocalCertificateSelectionCallback? ToCertificateSelection(X509Certificate2? clientCertificate) =>
        clientCertificate is null ? null : (_, _, _, _, _) => clientCertificate;

    private string SslConnectError(Exception failure) => _matchesSchannelBuild
        ? TlsFailureMessages.SchannelSslConnectError(failure)
        : TlsFailureMessages.OpenSslSslConnectError(failure);

    private string CaCertificateFileUnusable(string caCertificateFile) => _matchesSchannelBuild
        ? TlsFailureMessages.SchannelCaCertificateFileUnusable(caCertificateFile)
        : TlsFailureMessages.OpenSslCaCertificateFileUnusable(caCertificateFile);

    // The chain policy replaces the system store, from --cacert; null verifies against the
    // system store, with the --capath roots trusted beside it when there is no --cacert.
    // With --cacert the Schannel build checks revocation below the root unless
    // SkipRevocationCheck is set, so a private CA with no revocation endpoint fails with
    // exit 60 as in curl (ADR-0086); the OpenSSL build never checks it.
    private (X509ChainPolicy? ChainPolicy, X509Certificate2Collection AnchorsBesideSystemStore) ReadTrustAnchors()
    {
        if (_options.Insecure)
        {
            return (null, []);
        }

        var directoryAnchors = ReadCaCertificateDirectory();
        if (_options.CaCertificateFile is null)
        {
            return (null, directoryAnchors);
        }

        var chainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = _matchesSchannelBuild && !_options.SkipRevocationCheck
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck,
        };
        chainPolicy.CustomTrustStore.AddRange(ReadCaCertificateFile(_options.CaCertificateFile));
        chainPolicy.CustomTrustStore.AddRange(directoryAnchors);
        return (chainPolicy, []);
    }

    // The OpenSSL build refuses the whole file (exit 77) unless every CERTIFICATE block in
    // it parses and there is at least one; ImportFromPem skips a block whose body is not
    // base64, so the blocks imported are counted against the blocks begun. The Schannel
    // build trusts whatever parses.
    private X509Certificate2Collection ReadCaCertificateFile(string path)
    {
        var pem = File.ReadAllText(path);
        var certificates = new X509Certificate2Collection();
        if (_matchesSchannelBuild)
        {
            ImportParsableCertificates(certificates, pem);
            return certificates;
        }

        certificates.ImportFromPem(pem);
        if (certificates.Count == 0 || certificates.Count != pem.AsSpan().Count(PemCertificateBegin))
        {
            throw new CryptographicException("The file holds no certificate, or one that could not be decoded.");
        }

        return certificates;
    }

    private X509Certificate2Collection ReadCaCertificateDirectory()
    {
        var certificates = new X509Certificate2Collection();
        var directory = _options.CaCertificateDirectory;
        if (_matchesSchannelBuild || directory is null)
        {
            return certificates;
        }

        foreach (var file in ListFilesOrNothing(directory))
        {
            ImportParsableCertificates(certificates, ReadTextOrNothing(file));
        }

        return certificates;
    }

    // A --capath that is missing, is not a directory or cannot be listed adds nothing,
    // as in curl's OpenSSL build.
    private static string[] ListFilesOrNothing(string directory)
    {
        try
        {
            return Directory.GetFiles(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string ReadTextOrNothing(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    // ImportFromPem throws for a block that is base64 but not a certificate; what was
    // imported before it is kept.
    private static void ImportParsableCertificates(X509Certificate2Collection certificates, string pem)
    {
        try
        {
            certificates.ImportFromPem(pem);
        }
        catch (CryptographicException)
        {
            // The rest of the text is not a usable certificate; it adds nothing.
        }
    }

    // The server's own chain, rebuilt against the extra roots alone.
    private static bool ChainLeadsToAnyOf(X509Chain? chain, X509Certificate2Collection anchors)
    {
        if (chain is null || chain.ChainElements.Count == 0 || anchors.Count == 0)
        {
            return false;
        }

        using var anchoredChain = new X509Chain();
        anchoredChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        anchoredChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        anchoredChain.ChainPolicy.CustomTrustStore.AddRange(anchors);
        anchoredChain.ChainPolicy.ExtraStore.AddRange(chain.ChainPolicy.ExtraStore);
        foreach (var element in chain.ChainElements)
        {
            anchoredChain.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        return anchoredChain.Build(chain.ChainElements[0].Certificate);
    }

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

    private static SslProtocols ToSslProtocols(TlsMinimumVersion minimumVersion) => minimumVersion switch
    {
        TlsMinimumVersion.Tls12 => SslProtocols.Tls12 | SslProtocols.Tls13,
        TlsMinimumVersion.Tls13 => SslProtocols.Tls13,
        _ => SslProtocols.None,
    };

    // What the validation callback learned about the server's certificate: whether it
    // verified, OpenSSL's code for it, and the DER of the chain the event reports.
    private sealed record PeerVerification(bool Verified, long? VerifyResult, ReadOnlyMemory<byte>[] Chain)
    {
        internal static PeerVerification Unobserved { get; } = new(false, null, []);
    }
}
