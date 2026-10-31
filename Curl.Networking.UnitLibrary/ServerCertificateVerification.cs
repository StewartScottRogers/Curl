using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Verifies a server's certificate as curl does, for both TLS clients (ADR-0140, "The
/// verification hand-off"): reads the trust anchors <c>--cacert</c> and <c>--capath</c>
/// name, takes what <see cref="SslPolicyErrors" /> a chain has, drops what curl tolerates,
/// applies the name check the build runs, and says whether the certificate is accepted and,
/// if not, the exit code and message. <see cref="SslStreamTlsProvider" /> calls it from its
/// validation callback and <see cref="HandBuiltCertificateVerifier" /> from the hand-built
/// client's, so <c>-k</c>, exit 60 and every message are the same on both paths.
/// </summary>
/// <param name="options">The settings the handshake runs with.</param>
/// <param name="matchesSchannelBuild">
/// <see langword="true" /> to behave like curl's Schannel build, <see langword="false" /> like
/// its OpenSSL build.
/// </param>
/// <param name="timeProvider">Dates the OpenSSL verify result's validity checks.</param>
internal sealed class ServerCertificateVerification(TlsClientOptions options, bool matchesSchannelBuild, TimeProvider timeProvider)
{
    private const string PemCertificateBegin = "-----BEGIN CERTIFICATE-----";

    private static readonly Oid ServerAuthentication = new("1.3.6.1.5.5.7.3.1");

    /// <summary>
    /// The Schannel build's message for exit 35 when a revocation status could not be fetched
    /// and the chain is flagged offline (BL-3252; Windows' own text for 0x80092013).
    /// </summary>
    internal const string SchannelRevocationOffline =
        "schannel: next InitializeSecurityContext failed: CRYPT_E_REVOCATION_OFFLINE (0x80092013) - The revocation function was unable to check revocation because the revocation server was offline.";

    /// <summary>
    /// The Schannel build's message for exit 35 when a revocation status could not be fetched
    /// (BL-3252; Windows' own text for 0x80092012).
    /// </summary>
    internal const string SchannelNoRevocationCheck =
        "schannel: next InitializeSecurityContext failed: CRYPT_E_NO_REVOCATION_CHECK (0x80092012) - The revocation function was unable to check revocation for the certificate.";

    /// <summary>
    /// The Schannel build's message for exit 35 when, checked against the system store, a
    /// certificate in the server's chain is revoked (curl 8.22.0, measured, BL-3215).
    /// </summary>
    internal const string SchannelCertificateRevoked =
        "schannel: next InitializeSecurityContext failed: CRYPT_E_REVOKED (0x80092010) - The certificate is revoked.";

    /// <summary>
    /// Reads a chain's <see cref="X509Chain.ChainStatus" />, none without a chain, for
    /// <see cref="SchannelRevocationRefusal" />. A test seam: a revoked status cannot be built
    /// without a revocation server, so tests replace it.
    /// </summary>
    internal Func<X509Chain?, X509ChainStatus[]> ReadChainStatus { get; init; } = chain => chain?.ChainStatus ?? [];

    /// <summary>
    /// The CA bundles curl's <c>configure</c> looks for, in its order, the first that exists
    /// being the OpenSSL build's built-in bundle (ADR-1168).
    /// </summary>
    internal static readonly string[] ConfigureCaBundleCandidates =
    [
        "/etc/ssl/certs/ca-certificates.crt",
        "/etc/pki/tls/certs/ca-bundle.crt",
        "/usr/share/ssl/certs/ca-bundle.crt",
        "/usr/local/share/certs/ca-root-nss.crt",
        "/etc/ssl/cert.pem",
    ];

    /// <summary>
    /// Reads a process environment variable, for <see cref="DefaultCaBundleFile" />. A test seam.
    /// </summary>
    internal Func<string, string?> ReadEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>
    /// The bundles <see cref="DefaultCaBundleFile" /> looks for, in order. A test seam.
    /// </summary>
    internal IReadOnlyList<string> DefaultCaBundleCandidates { get; init; } = ConfigureCaBundleCandidates;

    private string? _defaultCaBundleFile;

    private bool _defaultCaBundleFileFound;

    /// <summary>
    /// The built-in CA bundle the OpenSSL build trusts in place of the system store, or
    /// <see langword="null" /> to trust the system store (ADR-1168). .NET's OpenSSL store reads
    /// <c>SSL_CERT_FILE</c> and <c>SSL_CERT_DIR</c> itself, and either set to the empty string
    /// leaves it with nothing, where curl's built-in bundle does not depend on them: then, and
    /// only in the OpenSSL build without <c>--cacert</c>, the first of
    /// <see cref="DefaultCaBundleCandidates" /> that exists is read as curl reads its bundle.
    /// </summary>
    internal string? DefaultCaBundleFile
    {
        get
        {
            if (!_defaultCaBundleFileFound)
            {
                _defaultCaBundleFile = FindDefaultCaBundleFile();
                _defaultCaBundleFileFound = true;
            }

            return _defaultCaBundleFile;
        }
    }

    private string? FindDefaultCaBundleFile() =>
        matchesSchannelBuild || options.HasCaCertificates || !EmptiesSystemStore()
            ? null
            : DefaultCaBundleCandidates.FirstOrDefault(File.Exists);

    private bool EmptiesSystemStore() =>
        ReadEnvironmentVariable("SSL_CERT_FILE") is "" || ReadEnvironmentVariable("SSL_CERT_DIR") is "";

    // --cacert, or else the built-in bundle that stands in for an emptied system store.
    private bool HasCaCertificates => options.HasCaCertificates || DefaultCaBundleFile is not null;

    private string? CaCertificateFile => options.CaCertificateFile ?? DefaultCaBundleFile;

    /// <summary>
    /// Reads the roots the chain may lead to. The chain policy replaces the system store,
    /// from <c>--cacert</c> or the OpenSSL build's <see cref="DefaultCaBundleFile" />; without
    /// either the chain is verified against the system
    /// store, with the <c>--capath</c> roots trusted beside it, and the chain policy is the
    /// Schannel build's revocation check (online, every certificate but the root, ADR-0930),
    /// or <see langword="null" /> under <c>--ssl-no-revoke</c> and in the OpenSSL build. With
    /// <c>--cacert</c> the Schannel build checks revocation below the root unless
    /// <see cref="TlsClientOptions.SkipRevocationCheck" /> is set, so a private CA with no
    /// revocation endpoint fails with exit 60 as in curl (ADR-0321); the OpenSSL build never
    /// checks it. The OpenSSL build then loads the <c>--crlfile</c> lists, as curl loads them
    /// after the <c>--cacert</c> file (BL-609); the Schannel build ignores <c>--crlfile</c>.
    /// Under <c>-k</c> nothing is read.
    /// </summary>
    /// <returns>
    /// The chain policy, or <see langword="null" />, the roots trusted beside the system store, and
    /// the <c>--crlfile</c> lists, or <see langword="null" /> when none are checked.
    /// </returns>
    /// <exception cref="IOException">The <c>--cacert</c> file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The <c>--cacert</c> file cannot be opened.</exception>
    /// <exception cref="CryptographicException">
    /// The OpenSSL build refuses the <c>--cacert</c> file's contents, or, as
    /// <see cref="CertificateRevocationListFileException" />, the <c>--crlfile</c>.
    /// </exception>
    internal (X509ChainPolicy? ChainPolicy, X509Certificate2Collection AnchorsBesideSystemStore, CertificateRevocationListFile? RevocationLists) ReadTrustAnchors()
    {
        if (options.Insecure)
        {
            return (null, [], null);
        }

        var (chainPolicy, anchorsBesideSystemStore) = ReadChainAnchors();
        return (chainPolicy, anchorsBesideSystemStore, ReadRevocationLists());
    }

    /// <summary>
    /// The build's exit code and message for trust anchors <see cref="ReadTrustAnchors" /> could
    /// not read: exit 82 for the <c>--crlfile</c>, exit 77 for the <c>--cacert</c> file.
    /// </summary>
    /// <param name="exception">What <see cref="ReadTrustAnchors" /> threw.</param>
    /// <returns>The exit code and the message curl prints.</returns>
    internal (CurlExitCode ExitCode, string Message) TrustAnchorsUnusable(Exception exception) =>
        exception is CertificateRevocationListFileException
            ? (CurlExitCode.SslCrlBadfile, TlsFailureMessages.OpenSslRevocationListFileUnusable(options.CertificateRevocationListFile!))
            : (CurlExitCode.SslCacertBadfile, CaCertificateFileUnusable());

    /// <summary>
    /// Verifies a chain apart from any handshake (ADR-1052): reads the trust anchors as a
    /// handshake does and judges the chain with the <see cref="HandBuiltCertificateVerifier" />
    /// the hand-built client hands its chain to, so <c>-k</c>, <c>--cacert</c>, <c>--capath</c>,
    /// <c>--crlfile</c>, <c>--pinnedpubkey</c>, exit 60 and every message are the handshake's.
    /// </summary>
    /// <param name="certificates">The DER certificates the server sent, its own first.</param>
    /// <param name="targetHost">The host the certificate is checked against.</param>
    /// <returns>The verdict, with the exit code and message a handshake would fail with.</returns>
    internal ServerCertificateChainVerdict VerifyChain(IReadOnlyList<ReadOnlyMemory<byte>> certificates, string targetHost)
    {
        ArgumentNullException.ThrowIfNull(certificates);
        ArgumentNullException.ThrowIfNull(targetHost);
        try
        {
            var (chainPolicy, anchorsBesideSystemStore, revocationLists) = ReadTrustAnchors();
            var verdict = new HandBuiltCertificateVerifier(this, chainPolicy, anchorsBesideSystemStore, revocationLists, targetHost)
                .Verify(new ServerCertificateChain([.. certificates.Select(der => der.ToArray())], targetHost, null));
            return verdict.IsAccepted ? ServerCertificateChainVerdict.Accepted : Rejection(((CurlExitCode, string))verdict.Rejection!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return Rejection(TrustAnchorsUnusable(exception));
        }
    }

    private static ServerCertificateChainVerdict Rejection((CurlExitCode ExitCode, string Message) failure) =>
        ServerCertificateChainVerdict.Rejected(failure.ExitCode, failure.Message);

    private CertificateRevocationListFile? ReadRevocationLists() =>
        matchesSchannelBuild || options.CertificateRevocationListFile is null
            ? null
            : CertificateRevocationListFile.Load(options.CertificateRevocationListFile);

    private (X509ChainPolicy? ChainPolicy, X509Certificate2Collection AnchorsBesideSystemStore) ReadChainAnchors()
    {
        var directoryAnchors = ReadCaCertificateDirectory();
        if (!HasCaCertificates)
        {
            return (SystemStoreChainPolicy(), directoryAnchors);
        }

        var chainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = matchesSchannelBuild && !options.SkipRevocationCheck
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck,
        };
        chainPolicy.CustomTrustStore.AddRange(ReadCaCertificates());
        chainPolicy.CustomTrustStore.AddRange(directoryAnchors);
        return (chainPolicy, []);
    }

    // The Schannel build checks the system store chain for revocation as Schannel's default
    // flags do: online, CRL distribution points and OCSP, every certificate but the root,
    // unless --ssl-no-revoke (ADR-0930). The OpenSSL build checks none: SslStream's default.
    private X509ChainPolicy? SystemStoreChainPolicy()
    {
        if (!matchesSchannelBuild || options.SkipRevocationCheck)
        {
            return null;
        }

        var chainPolicy = new X509ChainPolicy
        {
            RevocationMode = X509RevocationMode.Online,
            RevocationFlag = X509RevocationFlag.ExcludeRoot,
        };
        chainPolicy.ApplicationPolicy.Add(ServerAuthentication);
        return chainPolicy;
    }

    // The build's message for exit 77: the --cacert file cannot be used.
    private string CaCertificateFileUnusable() => matchesSchannelBuild
        ? TlsFailureMessages.SchannelCaCertificateFileUnusable(CaCertificateFile!)
        : TlsFailureMessages.OpenSslCaCertificateFileUnusable(CaCertificateFile!);

    /// <summary>
    /// Judges a certificate the server presented: what the handshake event reports about it,
    /// and whether it is accepted. A certificate <see cref="VerifyPeer" /> accepts, or any
    /// under <c>-k</c>, is then checked against <see cref="TlsClientOptions.PinnedPublicKey" />:
    /// a key the pin does not name is exit 90, so a certificate that fails both is exit 60, as
    /// curl reports it (ADR-0193, BL-608). The Schannel build with <c>--cacert</c> is the
    /// exception: it judges the pin first, so a certificate that fails both is exit 90
    /// (ADR-0504, BL-2270). Before the pin, a certificate
    /// <see cref="VerifyPeer" /> accepts is checked against the <c>--crlfile</c> lists, when
    /// there are any: a refusal is exit 60 with OpenSSL's verify result (ADR-0197, BL-609).
    /// </summary>
    /// <param name="errors">What was found wrong with the chain, as <see cref="SslStream" /> reports it.</param>
    /// <param name="chain">The chain built, if a certificate was presented.</param>
    /// <param name="targetHost">The host the certificate is checked against.</param>
    /// <param name="anchorsBesideSystemStore">The <c>--capath</c> roots trusted beside the system store.</param>
    /// <param name="revocationLists">The <c>--crlfile</c> lists <see cref="ReadTrustAnchors" /> read, or <see langword="null" />.</param>
    /// <param name="peerCertificates">The DER of what the server sent, its own certificate first.</param>
    /// <param name="stapledResponse">The OCSP response the server stapled, or <see langword="null" /> for none: the Schannel build lets a good or revoked one answer for the server's certificate.</param>
    /// <returns>The observation for the handshake event and the failure, or <see langword="null" /> to accept.</returns>
    internal (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) Judge(
        SslPolicyErrors errors,
        X509Chain? chain,
        string targetHost,
        X509Certificate2Collection anchorsBesideSystemStore,
        CertificateRevocationListFile? revocationLists,
        ReadOnlyMemory<byte>[] peerCertificates,
        byte[]? stapledResponse = null)
    {
        var stapled = StapledRevocationStatus(stapledResponse, chain, peerCertificates);
        errors = WithoutUnavailableRevocationAGoodStapleAnswers(errors, chain, stapled);
        var anchoredErrors = WithTheNameCheckCurlRuns(
            WithoutChainErrorsCurlTolerates(errors, chain, anchorsBesideSystemStore), chain, targetHost);
        var observed = ObservePeerVerification(anchoredErrors, chain, peerCertificates) with
        {
            RevocationCheckIncomplete = RevocationCheckIncomplete(chain, stapled),
        };
        (observed, var refusal) = Refusal(observed, anchoredErrors, chain, targetHost, revocationLists, stapled);
        if (JudgesPinnedPublicKeyBeforeVerification)
        {
            var pinned = JudgePinnedPublicKey(observed, peerCertificates);
            return (pinned.Observed, pinned.Failure ?? refusal);
        }

        return refusal is null ? JudgePinnedPublicKey(observed, peerCertificates) : (observed, refusal);
    }

    // The Schannel build checks revocation with CertGetCertificateChain on the context Schannel
    // attached the server's stapled OCSP response to, so a staple that says good or revoked
    // answers for the server's own certificate before any online check (CGR-014, ADR-0834).
    // A staple that says neither, or none, leaves the online check to decide; -k and
    // --ssl-no-revoke check nothing, and the OpenSSL build reads a staple only for --cert-status.
    private OcspStapleStatus? StapledRevocationStatus(byte[]? stapledResponse, X509Chain? chain, ReadOnlyMemory<byte>[] peerCertificates) =>
        !matchesSchannelBuild || options.Insecure || options.SkipRevocationCheck || stapledResponse is null || peerCertificates.Length == 0
            ? null
            : OcspStapleVerifier.Verify(stapledResponse, IssuerCandidates(chain, peerCertificates), timeProvider.GetUtcNow()).Status;

    // What the server sent, its own certificate first, then the chain built, which holds the
    // --cacert root a server that sends only its own certificate leaves out: the staple's issuer.
    private static byte[][] IssuerCandidates(X509Chain? chain, ReadOnlyMemory<byte>[] peerCertificates) =>
        [.. peerCertificates.Select(der => der.ToArray()), .. (chain?.ChainElements.Select(element => element.Certificate.RawData) ?? [])];

    // --ssl-revoke-best-effort let a revocation status no staple answered go unchecked.
    private bool RevocationCheckIncomplete(X509Chain? chain, OcspStapleStatus? stapled) =>
        !options.Insecure && stapled != OcspStapleStatus.Good && RevocationBestEffortTolerates(chain);

    // The first refusal: a revoked staple, the chain or name, then a --crlfile list. A list's
    // refusal is OpenSSL's verify result too, so %{ssl_verify_result} reports its code, 23 for
    // a revoked certificate and 12 for an expired list (measured with curl 8.18.0, CGR-168).
    private (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) Refusal(
        PeerVerification observed,
        SslPolicyErrors anchoredErrors,
        X509Chain? chain,
        string targetHost,
        CertificateRevocationListFile? revocationLists,
        OcspStapleStatus? stapled)
    {
        var refusal = RevokedStapleRefusal(stapled) ?? VerifyPeer(anchoredErrors, chain, targetHost, []);
        if (refusal is not null)
        {
            return (observed, refusal);
        }

        return revocationLists?.FirstRefusal(chain!, timeProvider.GetUtcNow()) is { } verifyResult
            ? (observed with { VerifyResult = verifyResult }, (CurlExitCode.PeerFailedVerification, TlsFailureMessages.OpenSslRevocationListRefusal(verifyResult)))
            : (observed, null);
    }

    // A good staple answers the revocation status the online check could not fetch.
    private SslPolicyErrors WithoutUnavailableRevocationAGoodStapleAnswers(SslPolicyErrors errors, X509Chain? chain, OcspStapleStatus? stapled) =>
        stapled == OcspStapleStatus.Good && HasOnlyUnavailableRevocationStatus(ReadChainStatus(chain))
            ? errors & ~SslPolicyErrors.RemoteCertificateChainErrors
            : errors;

    // A revoked staple fails as a revoked chain does, --ssl-revoke-best-effort or not (measured
    // with curl 8.21.0, BL-3826): exit 60 with --cacert, which curl's Schannel build checks
    // after the handshake, else the handshake's own CRYPT_E_REVOKED, exit 35.
    private (CurlExitCode ExitCode, string Message)? RevokedStapleRefusal(OcspStapleStatus? stapled) =>
        stapled != OcspStapleStatus.Revoked
            ? null
            : HasCaCertificates
                ? (CurlExitCode.PeerFailedVerification, TlsFailureMessages.SchannelChainRevoked)
                : (CurlExitCode.SslConnectError, SchannelCertificateRevoked);

    // With --cacert curl's Schannel build validates the chain itself after the handshake, and
    // checks the pin before that, so a wrong pin is exit 90 even where the chain would fail
    // (measured with curl 8.21.0, BL-2270; upstream test2035, test2038, test2042). Without
    // --cacert Schannel validates inside the handshake, before the pin.
    private bool JudgesPinnedPublicKeyBeforeVerification =>
        matchesSchannelBuild && HasCaCertificates;

    // The pin is checked only once the certificate is accepted, and only then does curl print
    // the " public key hash:" line (ADR-0336, BL-877).
    private (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) JudgePinnedPublicKey(
        PeerVerification observed,
        ReadOnlyMemory<byte>[] peerCertificates)
    {
        var refusal = PinnedPublicKey.Refusal(options.PinnedPublicKey, peerCertificates);
        return (
            observed with
            {
                PinnedPublicKeyHash = PinnedPublicKey.ReportedHash(options.PinnedPublicKey, peerCertificates),
                PinnedPublicKeyRefused = refusal is not null,
            },
            refusal);
    }

    // Reached only for a chain VerifyPeer accepted without -k, so a chain was built.
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
        if (options.Insecure && !options.ChecksHostNameWhenInsecure)
        {
            return null;
        }

        errors = WithTheNameCheckCurlRuns(
            WithoutChainErrorsCurlTolerates(errors, chain, anchorsBesideSystemStore), chain, targetHost);
        if (options.Insecure)
        {
            // CURLOPT_SSL_VERIFYHOST without CURLOPT_SSL_VERIFYPEER: only the name counts.
            errors &= SslPolicyErrors.RemoteCertificateNameMismatch;
        }

        if (errors == SslPolicyErrors.None)
        {
            return null;
        }

        return matchesSchannelBuild
            ? SchannelRefusal(errors, chain, targetHost)
            : (CurlExitCode.PeerFailedVerification, TlsFailureMessages.OpenSslPeerFailedVerification(errors, chain, targetHost));
    }

    /// <summary>
    /// Returns whether a chain's faults are all ones <c>--ssl-revoke-best-effort</c> tolerates:
    /// its revocation status is unknown or could not be fetched, and nothing else is wrong.
    /// </summary>
    /// <param name="chainStatus">The chain's <see cref="X509Chain.ChainStatus" />.</param>
    /// <returns>
    /// <see langword="true" /> when there is at least one fault and every fault is
    /// <see cref="X509ChainStatusFlags.RevocationStatusUnknown" /> or
    /// <see cref="X509ChainStatusFlags.OfflineRevocation" />.
    /// </returns>
    internal static bool HasOnlyUnavailableRevocationStatus(X509ChainStatus[] chainStatus) =>
        chainStatus.Length > 0
        && chainStatus.All(status =>
            (status.Status & ~(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation)) == 0);

    /// <summary>
    /// Whether the Schannel build, checking against the system store, fails the handshake
    /// with <see cref="SchannelCertificateRevoked" /> (exit 35): the host name matches and the
    /// chain's only fault, beside a revocation status that could not be fetched for another
    /// certificate, is that one of its certificates, the server's own or an intermediate, is
    /// revoked.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chainStatus">The chain's <see cref="X509Chain.ChainStatus" />.</param>
    /// <returns><see langword="true" /> when Schannel reports <c>CRYPT_E_REVOKED</c>.</returns>
    internal static bool IsSchannelCertificateRevoked(SslPolicyErrors errors, X509ChainStatus[] chainStatus) =>
        errors == SslPolicyErrors.RemoteCertificateChainErrors
        && (chainStatus.Aggregate(X509ChainStatusFlags.NoError, (all, next) => all | next.Status)
            & ~(X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.OfflineRevocation))
            == X509ChainStatusFlags.Revoked;

    /// <summary>
    /// The Schannel build's refusal of a chain checked against the system store, unless
    /// <c>--ssl-no-revoke</c>, in which a certificate is revoked: exit 35 with
    /// <see cref="SchannelCertificateRevoked" />. <c>--ssl-revoke-best-effort</c> does not
    /// tolerate it; the OpenSSL build never checks revocation there.
    /// </summary>
    /// <param name="errors">What the verification found wrong.</param>
    /// <param name="chainStatus">The chain's <see cref="X509Chain.ChainStatus" />.</param>
    /// <returns>The exit code and message, or <see langword="null" /> when this is not the refusal.</returns>
    internal (CurlExitCode ExitCode, string Message)? SchannelRevocationRefusal(SslPolicyErrors errors, X509ChainStatus[] chainStatus) =>
        !matchesSchannelBuild || HasCaCertificates || options.SkipRevocationCheck
            ? null
            : IsSchannelCertificateRevoked(errors, chainStatus)
                ? (CurlExitCode.SslConnectError, SchannelCertificateRevoked)
                : SchannelRevocationUnavailableRefusal(errors, chainStatus);

    // Without --ssl-revoke-best-effort, Schannel fails the handshake when a revocation status
    // could not be fetched: CRYPT_E_REVOCATION_OFFLINE when the chain is flagged offline,
    // CRYPT_E_NO_REVOCATION_CHECK otherwise (BL-3252; texts are Windows' own).
    private (CurlExitCode ExitCode, string Message)? SchannelRevocationUnavailableRefusal(
        SslPolicyErrors errors, X509ChainStatus[] chainStatus) =>
        errors != SslPolicyErrors.RemoteCertificateChainErrors || !HasOnlyUnavailableRevocationStatus(chainStatus)
            ? null
            : (CurlExitCode.SslConnectError,
                chainStatus.Any(status => status.Status.HasFlag(X509ChainStatusFlags.OfflineRevocation))
                    ? SchannelRevocationOffline
                    : SchannelNoRevocationCheck);

    // The Schannel build's answer for a certificate SslStream found fault with: a revoked
    // chain is the handshake's failure, anything else the peer verification's.
    private (CurlExitCode ExitCode, string Message) SchannelRefusal(SslPolicyErrors errors, X509Chain? chain, string targetHost) =>
        SchannelRevocationRefusal(errors, ReadChainStatus(chain)) ?? SchannelPeerVerificationFailure(errors, chain, targetHost);

    // The Schannel build's answer for a certificate SslStream found fault with, but revocation.
    private (CurlExitCode ExitCode, string Message) SchannelPeerVerificationFailure(
        SslPolicyErrors errors,
        X509Chain? chain,
        string targetHost)
    {
        var hasCaCertificateFile = HasCaCertificates;
        return !hasCaCertificateFile && TlsFailureMessages.IsSchannelCertificateExpired(errors, chain)
            ? (CurlExitCode.SslConnectError, TlsFailureMessages.SchannelCertificateExpired)
            : (CurlExitCode.PeerFailedVerification,
                TlsFailureMessages.SchannelPeerFailedVerification(errors, chain, targetHost, hasCaCertificateFile));
    }

    // The chain's errors do not count when it leads to a --capath root trusted beside the
    // system store, when --ssl-revoke-best-effort tolerates every one of them, or when the
    // --cacert file anchors it: on the Schannel build by listing the server's own (leaf)
    // certificate, on the OpenSSL build by listing any certificate of the chain.
    private SslPolicyErrors WithoutChainErrorsCurlTolerates(
        SslPolicyErrors errors,
        X509Chain? chain,
        X509Certificate2Collection anchorsBesideSystemStore) =>
        ChainLeadsToAnyOf(chain, anchorsBesideSystemStore) || RevocationBestEffortTolerates(chain) || CaCertificateFileAnchorsChain(chain)
            ? errors & ~SslPolicyErrors.RemoteCertificateChainErrors
            : errors;

    // curl's Schannel build builds the --cacert chain with an exclusive-root engine holding
    // the file's certificates, where a leaf the file lists is itself a trust anchor: a chain
    // .NET finds partial or untrusted only for want of a root is accepted (measured with curl
    // 8.22.0, tls-wrong-ca, BL-3289, ADR-0797).
    //
    // curl's OpenSSL build sets X509_V_FLAG_PARTIAL_CHAIN, so there any certificate of the
    // chain the file lists, leaf or intermediate, anchors it (measured with curl 8.18.0 and
    // OpenSSL 3.5.5, BL-3335).
    private bool CaCertificateFileAnchorsChain(X509Chain? chain) =>
        HasCaCertificates
        && chain is not null
        && chain.ChainElements.Count > 0
        && HasOnlyMissingRootStatus(ReadChainStatus(chain))
        && ListsAnAnchorCandidate(chain);

    private bool ListsAnAnchorCandidate(X509Chain chain)
    {
        var candidates = matchesSchannelBuild
            ? chain.ChainElements.Cast<X509ChainElement>().Take(1)
            : chain.ChainElements.Cast<X509ChainElement>();
        return candidates.Any(element => ListsCertificate(chain.ChainPolicy.CustomTrustStore, element.Certificate));
    }

    private static bool HasOnlyMissingRootStatus(X509ChainStatus[] chainStatus) =>
        chainStatus.All(status => (status.Status & ~(X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.UntrustedRoot)) == 0);

    private static bool ListsCertificate(X509Certificate2Collection certificates, X509Certificate2 certificate) =>
        certificates.Any(listed => listed.RawDataMemory.Span.SequenceEqual(certificate.RawDataMemory.Span));

    // curl's Schannel build, under --ssl-revoke-best-effort, masks CERT_TRUST_REVOCATION_STATUS_UNKNOWN
    // and CERT_TRUST_IS_OFFLINE_REVOCATION out of the chain's trust errors (measured, BL-490).
    private bool RevocationBestEffortTolerates(X509Chain? chain) =>
        matchesSchannelBuild
        && options.RevocationCheckBestEffort
        && chain is not null
        && HasOnlyUnavailableRevocationStatus(chain.ChainStatus);

    // Where curl's name check and .NET's disagree, the build curl's answer stands.
    private SslPolicyErrors WithTheNameCheckCurlRuns(SslPolicyErrors errors, X509Chain? chain, string targetHost) =>
        matchesSchannelBuild
            ? WithoutNameMismatchSchannelAccepts(errors, chain, targetHost)
            : WithNameMismatchOpenSslFinds(errors, chain, targetHost);

    // curl's OpenSSL build never matches a host name by the common name of a certificate
    // whose subjectAltName holds only IP addresses, which .NET on Windows does (BL-460).
    private static SslPolicyErrors WithNameMismatchOpenSslFinds(SslPolicyErrors errors, X509Chain? chain, string targetHost) =>
        chain is { ChainElements.Count: > 0 }
        && OpenSslCommonNameRefusal.RefusesHostName(chain.ChainElements[0].Certificate, targetHost)
            ? errors | SslPolicyErrors.RemoteCertificateNameMismatch
            : errors;

    // With --cacert curl's Schannel build checks the name itself and, for a certificate
    // with no DNS subjectAltName, matches the common name, which .NET's check does not
    // (BL-415). Without --cacert Schannel's own check stands.
    private SslPolicyErrors WithoutNameMismatchSchannelAccepts(SslPolicyErrors errors, X509Chain? chain, string targetHost) =>
        HasCaCertificates
        && errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch)
        && SchannelCommonNameCheck.CommonNameMatches(chain!.ChainElements[0].Certificate, targetHost)
            ? errors & ~SslPolicyErrors.RemoteCertificateNameMismatch
            : errors;

    // Taken while the chain is alive, whether or not -k lets the handshake go on, because
    // SslStream disposes the chain once its callback returns.
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
            RefusesTheName(anchoredErrors)
                ? OpenSslVerifyResult.Unspecified
                : OpenSslVerifyResult.Of(anchoredErrors, chain, timeProvider.GetUtcNow()),
            reportedChain);
    }

    // curl checks the name before the chain and, when the name fails, reports
    // X509_V_ERR_UNSPECIFIED whatever the chain's code; -k checks no name (BL-661).
    private bool RefusesTheName(SslPolicyErrors anchoredErrors) =>
        !options.Insecure && (anchoredErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0;

    private static ReadOnlyMemory<byte>[] ListChainElements(X509Chain chain)
    {
        var elements = new List<ReadOnlyMemory<byte>>();
        foreach (var element in chain.ChainElements)
        {
            elements.Add(element.Certificate.RawData);
        }

        return [.. elements];
    }

    // The OpenSSL build refuses the whole file (exit 77) unless every CERTIFICATE block in
    // it parses and there is at least one; ImportFromPem skips a block whose body is not
    // base64, so the blocks imported are counted against the blocks begun. The Schannel
    // build trusts whatever parses.
    // A CURLOPT_CAINFO_BLOB is read as the file would be, and wins over it (ADR-0704).
    private X509Certificate2Collection ReadCaCertificates()
    {
        var pem = options.CaCertificateBlob is { } blob
            ? Encoding.ASCII.GetString(blob.Span)
            : ReadCaCertificateFile(CaCertificateFile!);
        var certificates = new X509Certificate2Collection();
        if (matchesSchannelBuild)
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

    // An empty --cacert names no file, so it is unusable (exit 77) rather than the
    // ArgumentException File.ReadAllText throws for it (GF-0097).
    private static string ReadCaCertificateFile(string path) =>
        path.Length == 0
            ? throw new FileNotFoundException("The CA certificate file name is empty.")
            : File.ReadAllText(path);

    private X509Certificate2Collection ReadCaCertificateDirectory()
    {
        var certificates = new X509Certificate2Collection();
        var directories = options.CaCertificateDirectory;
        if (matchesSchannelBuild || directories is null)
        {
            return certificates;
        }

        foreach (var directory in directories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var subjectHash in SubjectHashesNamedIn(directory))
            {
                ImportHashedCertificates(certificates, directory, subjectHash);
            }
        }

        return certificates;
    }

    // OpenSSL's hashed directory lookup (by_dir.c, ADR-0836) opens only files named
    // <subject hash>.<n>, as c_rehash and openssl rehash name them; any other file adds nothing
    // (CGR-111).
    private static IEnumerable<string> SubjectHashesNamedIn(string directory) =>
        ListFilesOrNothing(directory)
            .Select(Path.GetFileName)
            .Where(IsHashedCertificateFileName)
            .Select(name => name![..8])
            .Distinct(StringComparer.Ordinal);

    private static bool IsHashedCertificateFileName(string? name) =>
        name is { Length: > 9 }
        && name[8] == '.'
        && name.AsSpan(0, 8).ContainsAnyExcept("0123456789abcdef") is false
        && name.AsSpan(9).ContainsAnyExceptInRange('0', '9') is false;

    // OpenSSL reads <hash>.0, <hash>.1 and on until a number is missing, and finds in them only
    // the certificates whose subject has that hash.
    private static void ImportHashedCertificates(X509Certificate2Collection certificates, string directory, string subjectHash)
    {
        for (var number = 0; File.Exists(Path.Combine(directory, $"{subjectHash}.{number}")); number++)
        {
            var inFile = new X509Certificate2Collection();
            ImportParsableCertificates(inFile, ReadTextOrNothing(Path.Combine(directory, $"{subjectHash}.{number}")));
            certificates.AddRange(inFile.Where(certificate => OpenSslSubjectNameHash.Of(certificate.SubjectName) == subjectHash).ToArray());
        }
    }

    // A --capath that is empty, malformed, missing, not a directory or cannot be listed adds
    // nothing, as in curl's OpenSSL build (GF-0097).
    private static string[] ListFilesOrNothing(string directory)
    {
        try
        {
            return Directory.GetFiles(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
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
}
