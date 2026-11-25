using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Reads the <c>--cacert</c> and <c>--capath</c> certificates and rebuilds a chain against them.
/// </summary>
internal sealed partial class ServerCertificateVerification
{
    // The thumbprints of the --cacert and --capath certificates whose OpenSSL settings refuse
    // server authentication; empty in the Schannel build and under -k.
    private IReadOnlySet<string> rejectedCaCertificates = new HashSet<string>();

    private const long CertificateRejectedVerifyResult = 28;

    private const string OpenSslCertificateRejectedRefusal = "SSL certificate OpenSSL verify result: certificate rejected (28)";

    // The OpenSSL build refuses the whole file (exit 77) unless every CERTIFICATE and TRUSTED
    // CERTIFICATE block in it parses and there is at least one. The Schannel build trusts
    // whatever CERTIFICATE blocks parse.
    // A CURLOPT_CAINFO_BLOB is read as the file would be, and wins over it (ADR-0704).
    // The --cacert certificates, then the --capath ones.
    private X509Certificate2Collection ReadCaCertificates(OpenSslCaFile directory)
    {
        var pem = options.CaCertificateBlob is { } blob
            ? Encoding.ASCII.GetString(blob.Span)
            : ReadCaCertificateFile(CaCertificateFile!);
        if (matchesSchannelBuild)
        {
            var certificates = new X509Certificate2Collection();
            ImportParsableCertificates(certificates, pem);
            return certificates;
        }

        var trusted = OpenSslCaFile.Read(pem).FollowedBy(directory);
        rejectedCaCertificates = trusted.RejectedForServerAuthentication;
        return trusted.Certificates;
    }

    // OpenSSL, with partial chains allowed as curl sets it, stops at the lowest certificate above
    // the server's own that the trust store lists, else at the server's own, and fails the chain
    // when that one's settings refuse server authentication (BL-5879). With --capath roots beside
    // the system store the trust store listing them is that of the chain rebuilt against them
    // (BL-5917).
    private (CurlExitCode ExitCode, string Message)? RejectedCaCertificateRefusal(X509Chain? chain, X509Certificate2Collection anchorsBesideSystemStore) =>
        IsDecidedByARejectedCertificate(chain, anchorsBesideSystemStore)
            ? (CurlExitCode.PeerFailedVerification, OpenSslCertificateRejectedRefusal)
            : null;

    private bool IsDecidedByARejectedCertificate(X509Chain? chain, X509Certificate2Collection anchorsBesideSystemStore)
    {
        if (rejectedCaCertificates.Count == 0 || chain is not { ChainElements.Count: > 0 })
        {
            return false;
        }

        using var anchored = RebuildAgainstAnchors(chain, anchorsBesideSystemStore, requireServerAuthentication: false, out _);
        return anchored is null
            ? IsRejected(DecidingCertificate(chain))
            : IsRejectedBesideTheSystemStore(DecidingCertificate(anchored), chain);
    }

    private bool IsRejected(X509Certificate2 certificate) => rejectedCaCertificates.Contains(certificate.Thumbprint);

    private bool IsRejectedBesideTheSystemStore(X509Certificate2 certificate, X509Chain systemChain) =>
        IsRejected(certificate) && !IsRootOfASoundChain(systemChain, certificate);

    // A root the system store's chain ends at without fault is in OpenSSL's default CA file, which
    // it loads before it looks in the --capath, so the file's copy and not the directory's counts.
    private static bool IsRootOfASoundChain(X509Chain chain, X509Certificate2 certificate) =>
        chain.ChainStatus.Length == 0 && chain.ChainElements[^1].Certificate.Thumbprint == certificate.Thumbprint;

    private static X509Certificate2 DecidingCertificate(X509Chain chain) =>
        chain.ChainElements
            .Skip(1)
            .Select(element => element.Certificate)
            .FirstOrDefault(certificate => chain.ChainPolicy.CustomTrustStore.Any(anchor => anchor.Thumbprint == certificate.Thumbprint))
        ?? chain.ChainElements[0].Certificate;

    // An empty --cacert names no file, so it is unusable (exit 77) rather than the
    // ArgumentException File.ReadAllText throws for it (GF-0097).
    private static string ReadCaCertificateFile(string path) =>
        path.Length == 0
            ? throw new FileNotFoundException("The CA certificate file name is empty.")
            : File.ReadAllText(path);

    // The OpenSSL build reads a --capath file's TRUSTED CERTIFICATE blocks and their settings as
    // it reads --cacert's, an earlier directory's or file's copy winning (BL-5917).
    private OpenSslCaFile ReadCaCertificateDirectory() =>
        OpenSslCaFile.Of(CaCertificateDirectories().SelectMany(ReadHashedDirectory));

    private string[] CaCertificateDirectories() =>
        matchesSchannelBuild || options.CaCertificateDirectory is not { } directories
            ? []
            : directories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

    private static IEnumerable<(X509Certificate2 Certificate, bool Rejects)> ReadHashedDirectory(string directory) =>
        SubjectHashesNamedIn(directory).SelectMany(subjectHash => ReadHashedCertificates(directory, subjectHash));

    // OpenSSL's hashed directory lookup (by_dir.c, ADR-0836) opens only files named
    // <subject hash>.<n>, as c_rehash and openssl rehash name them; any other file adds nothing
    // (CGR-111).
    private static IEnumerable<string> SubjectHashesNamedIn(string directory) =>
        ListFilesOrNothing(directory)
            .Select(Path.GetFileName)
            .Where(IsHashedCertificateFileName)
            .Select(name => name![..8])
            .Distinct(StringComparer.Ordinal);

    // Length >= 10: eight hash characters, the dot and at least one digit; a bare "<hash>." never names a file OpenSSL reads.
    private static bool IsHashedCertificateFileName(string? name) =>
        name is { Length: >= 10 }
        && name[8] == '.'
        && name.AsSpan(0, 8).ContainsAnyExcept("0123456789abcdef") is false
        && name.AsSpan(9).ContainsAnyExceptInRange('0', '9') is false;

    // OpenSSL reads <hash>.0, <hash>.1 and on until a number is missing, and finds in them only
    // the certificates whose subject has that hash.
    private static IEnumerable<(X509Certificate2 Certificate, bool Rejects)> ReadHashedCertificates(string directory, string subjectHash)
    {
        for (var number = 0; File.Exists(Path.Combine(directory, $"{subjectHash}.{number}")); number++)
        {
            var text = ReadTextOrNothing(Path.Combine(directory, $"{subjectHash}.{number}"));
            foreach (var block in OpenSslCaFile.ReadUpToAnUndecodableBlock(text))
            {
                if (OpenSslSubjectNameHash.Of(block.Certificate.SubjectName) == subjectHash)
                {
                    yield return block;
                }
            }
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

    // The server's own chain, rebuilt against the extra roots alone, for server authentication.
    private static bool ChainLeadsToAnyOf(X509Chain? chain, X509Certificate2Collection anchors) =>
        BuildAgainstAnchors(chain, anchors, requireServerAuthentication: true);

    // The chain leads to the anchors, but not for server authentication: OpenSSL's TLS server
    // purpose check fails it with "unsuitable certificate purpose (26)" (BL-5310).
    private static bool AnchorsRefuseServerPurpose(X509Chain? chain, X509Certificate2Collection anchors) =>
        BuildAgainstAnchors(chain, anchors, requireServerAuthentication: false)
        && !BuildAgainstAnchors(chain, anchors, requireServerAuthentication: true);

    private static bool BuildAgainstAnchors(X509Chain? chain, X509Certificate2Collection anchors, bool requireServerAuthentication)
    {
        using var anchoredChain = RebuildAgainstAnchors(chain, anchors, requireServerAuthentication, out var built);
        return built || (anchoredChain is not null && IsPartialChainAtListedAnchor(anchoredChain));
    }

    // The server's chain built against the anchors alone, built whether or not it succeeds so its
    // statuses give the reason it failed; null when there is no chain or no anchor. The caller disposes it.
    private static X509Chain? RebuildAgainstAnchors(X509Chain? chain, X509Certificate2Collection anchors, bool requireServerAuthentication, out bool built)
    {
        built = false;
        if (chain is null || chain.ChainElements.Count == 0 || anchors.Count == 0)
        {
            return null;
        }

        // OpenSSL follows no AIA link, so a missing intermediate is not downloaded here either (BL-5448);
        // only the OpenSSL build reads --capath anchors, the Schannel build ignores them.
        var anchoredChain = new X509Chain();
        anchoredChain.ChainPolicy.DisableCertificateDownloads = true;
        anchoredChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        anchoredChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        if (requireServerAuthentication)
        {
            anchoredChain.ChainPolicy.ApplicationPolicy.Add(ServerAuthentication);
        }

        anchoredChain.ChainPolicy.CustomTrustStore.AddRange(anchors);
        anchoredChain.ChainPolicy.ExtraStore.AddRange(chain.ChainPolicy.ExtraStore);
        foreach (var element in chain.ChainElements)
        {
            anchoredChain.ChainPolicy.ExtraStore.Add(element.Certificate);
        }

        built = anchoredChain.Build(chain.ChainElements[0].Certificate);
        return anchoredChain;
    }

    // OpenSSL curl sets X509_V_FLAG_PARTIAL_CHAIN, so a --capath directory holding only an
    // intermediate CA anchors a chain through it: .NET finds that chain partial for want of a
    // self-signed root, and only a broken name constraint or the like makes it a failure (BL-5439).
    private static bool IsPartialChainAtListedAnchor(X509Chain anchoredChain) =>
        anchoredChain.ChainElements.Count > 0
        && HasOnlyMissingRootStatus(anchoredChain.ChainStatus)
        && ListsAnAnchorCandidate(anchoredChain)
        && CertificateNameConstraints.FindViolation(anchoredChain) == 0;

    // OpenSSL names the reason from the chain it built against the --capath roots, so an expired
    // leaf under one of them is "certificate has expired (10)", not the system store's "unable to
    // get local issuer certificate (20)" (BL-5395). Null where the system store's chain stands: when
    // the rebuilt chain does not end at one of the anchors the server's chain does not lead to a
    // --capath root, and OpenSSL's default CA bundle beside it names the reason (BL-5425).
    private X509Chain? ReasonChainFromAnchors(SslPolicyErrors errors, X509Chain? chain, X509Certificate2Collection anchors)
    {
        if (options.Insecure || matchesSchannelBuild || (errors & SslPolicyErrors.RemoteCertificateChainErrors) == 0)
        {
            return null;
        }

        var rebuilt = RebuildAgainstAnchors(chain, anchors, requireServerAuthentication: true, out _);
        if (rebuilt is not null && !EndsAtAnchor(rebuilt, anchors))
        {
            rebuilt.Dispose();
            return null;
        }

        return rebuilt;
    }

    private static bool EndsAtAnchor(X509Chain rebuilt, X509Certificate2Collection anchors) =>
        anchors.Any(anchor => anchor.Thumbprint == rebuilt.ChainElements[^1].Certificate.Thumbprint);
}
