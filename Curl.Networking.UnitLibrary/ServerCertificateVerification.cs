using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

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

    /// <summary>
    /// Reads the roots the chain may lead to. The chain policy replaces the system store,
    /// from <c>--cacert</c>; <see langword="null" /> verifies against the system store, with
    /// the <c>--capath</c> roots trusted beside it when there is no <c>--cacert</c>. With
    /// <c>--cacert</c> the Schannel build checks revocation below the root unless
    /// <see cref="TlsClientOptions.SkipRevocationCheck" /> is set, so a private CA with no
    /// revocation endpoint fails with exit 60 as in curl (ADR-0086); the OpenSSL build never
    /// checks it.
    /// </summary>
    /// <returns>The chain policy, or <see langword="null" />, and the roots trusted beside the system store.</returns>
    /// <exception cref="IOException">The <c>--cacert</c> file cannot be read.</exception>
    /// <exception cref="UnauthorizedAccessException">The <c>--cacert</c> file cannot be opened.</exception>
    /// <exception cref="CryptographicException">The OpenSSL build refuses the <c>--cacert</c> file's contents.</exception>
    internal (X509ChainPolicy? ChainPolicy, X509Certificate2Collection AnchorsBesideSystemStore) ReadTrustAnchors()
    {
        if (options.Insecure)
        {
            return (null, []);
        }

        var directoryAnchors = ReadCaCertificateDirectory();
        if (options.CaCertificateFile is null)
        {
            return (null, directoryAnchors);
        }

        var chainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = matchesSchannelBuild && !options.SkipRevocationCheck
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck,
        };
        chainPolicy.CustomTrustStore.AddRange(ReadCaCertificateFile(options.CaCertificateFile));
        chainPolicy.CustomTrustStore.AddRange(directoryAnchors);
        return (chainPolicy, []);
    }

    /// <summary>The build's message for exit 77: the <c>--cacert</c> file cannot be used.</summary>
    /// <returns>The message curl prints.</returns>
    internal string CaCertificateFileUnusable() => matchesSchannelBuild
        ? TlsFailureMessages.SchannelCaCertificateFileUnusable(options.CaCertificateFile!)
        : TlsFailureMessages.OpenSslCaCertificateFileUnusable(options.CaCertificateFile!);

    /// <summary>
    /// Judges a certificate the server presented: what the handshake event reports about it,
    /// and whether it is accepted. A certificate <see cref="VerifyPeer" /> accepts, or any
    /// under <c>-k</c>, is then checked against <see cref="TlsClientOptions.PinnedPublicKey" />:
    /// a key the pin does not name is exit 90, so a certificate that fails both is exit 60, as
    /// curl reports it (ADR-0192, BL-608).
    /// </summary>
    /// <param name="errors">What was found wrong with the chain, as <see cref="SslStream" /> reports it.</param>
    /// <param name="chain">The chain built, if a certificate was presented.</param>
    /// <param name="targetHost">The host the certificate is checked against.</param>
    /// <param name="anchorsBesideSystemStore">The <c>--capath</c> roots trusted beside the system store.</param>
    /// <param name="peerCertificates">The DER of what the server sent, its own certificate first.</param>
    /// <returns>The observation for the handshake event and the failure, or <see langword="null" /> to accept.</returns>
    internal (PeerVerification Observed, (CurlExitCode ExitCode, string Message)? Failure) Judge(
        SslPolicyErrors errors,
        X509Chain? chain,
        string targetHost,
        X509Certificate2Collection anchorsBesideSystemStore,
        ReadOnlyMemory<byte>[] peerCertificates)
    {
        var anchoredErrors = WithTheNameCheckCurlRuns(
            WithoutChainErrorsCurlTolerates(errors, chain, anchorsBesideSystemStore), chain, targetHost);
        return (
            ObservePeerVerification(anchoredErrors, chain, peerCertificates),
            VerifyPeer(anchoredErrors, chain, targetHost, []) ?? PinnedPublicKey.Refusal(options.PinnedPublicKey, peerCertificates));
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
        if (options.Insecure)
        {
            return null;
        }

        errors = WithTheNameCheckCurlRuns(
            WithoutChainErrorsCurlTolerates(errors, chain, anchorsBesideSystemStore), chain, targetHost);
        if (errors == SslPolicyErrors.None)
        {
            return null;
        }

        return matchesSchannelBuild
            ? SchannelPeerVerificationFailure(errors, chain, targetHost)
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

    // The Schannel build's answer for a certificate SslStream found fault with.
    private (CurlExitCode ExitCode, string Message) SchannelPeerVerificationFailure(
        SslPolicyErrors errors,
        X509Chain? chain,
        string targetHost)
    {
        var hasCaCertificateFile = options.CaCertificateFile is not null;
        return !hasCaCertificateFile && TlsFailureMessages.IsSchannelCertificateExpired(errors, chain)
            ? (CurlExitCode.SslConnectError, TlsFailureMessages.SchannelCertificateExpired)
            : (CurlExitCode.PeerFailedVerification,
                TlsFailureMessages.SchannelPeerFailedVerification(errors, chain, targetHost, hasCaCertificateFile));
    }

    // The chain's errors do not count when it leads to a --capath root trusted beside the
    // system store, or when --ssl-revoke-best-effort tolerates every one of them.
    private SslPolicyErrors WithoutChainErrorsCurlTolerates(
        SslPolicyErrors errors,
        X509Chain? chain,
        X509Certificate2Collection anchorsBesideSystemStore) =>
        ChainLeadsToAnyOf(chain, anchorsBesideSystemStore) || RevocationBestEffortTolerates(chain)
            ? errors & ~SslPolicyErrors.RemoteCertificateChainErrors
            : errors;

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
        options.CaCertificateFile is not null
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
            OpenSslVerifyResult.Of(anchoredErrors, chain, timeProvider.GetUtcNow()),
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

    // The OpenSSL build refuses the whole file (exit 77) unless every CERTIFICATE block in
    // it parses and there is at least one; ImportFromPem skips a block whose body is not
    // base64, so the blocks imported are counted against the blocks begun. The Schannel
    // build trusts whatever parses.
    private X509Certificate2Collection ReadCaCertificateFile(string path)
    {
        var pem = File.ReadAllText(path);
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

    private X509Certificate2Collection ReadCaCertificateDirectory()
    {
        var certificates = new X509Certificate2Collection();
        var directory = options.CaCertificateDirectory;
        if (matchesSchannelBuild || directory is null)
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
}
