using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// The certificate revocation lists of a <c>--crlfile</c>, and the check curl's OpenSSL build
/// makes with them (ADR-0197, BL-609). curl loads the file with <c>X509_load_crl_file</c>
/// (PEM) and verifies with <c>X509_V_FLAG_CRL_CHECK | X509_V_FLAG_CRL_CHECK_ALL</c>, so every
/// certificate of the chain, the root included, needs a list from its issuer. The BCL cannot
/// hand a list to <see cref="X509Chain" />, so the check is made here on the chain
/// <see cref="X509Chain" /> built: each certificate, from the server's own to the root, is
/// checked as OpenSSL's <c>check_crl</c> and <c>cert_crl</c> do, and the first failure is the
/// verify result curl prints. curl's Schannel build ignores <c>--crlfile</c> (measured,
/// BL-609), so only the OpenSSL build loads one.
/// </summary>
internal sealed class CertificateRevocationListFile
{
    private const string PemLabel = "X509 CRL";

    private readonly IReadOnlyList<CertificateRevocationList> _lists;

    private CertificateRevocationListFile(IReadOnlyList<CertificateRevocationList> lists) => _lists = lists;

    /// <summary>
    /// Reads every PEM <c>X509 CRL</c> block of the file; any other text and any other block
    /// are passed over, as OpenSSL's PEM reader passes them.
    /// </summary>
    /// <param name="path">The <c>--crlfile</c> path.</param>
    /// <returns>The lists.</returns>
    /// <exception cref="CertificateRevocationListFileException">
    /// The file cannot be read, holds no list, or holds a list that does not decode: curl's exit 82.
    /// </exception>
    public static CertificateRevocationListFile Load(string path)
    {
        try
        {
            return new CertificateRevocationListFile(DecodeAll(File.ReadAllText(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or AsnContentException or CryptographicException)
        {
            throw new CertificateRevocationListFileException(path, exception);
        }
    }

    /// <summary>
    /// Checks every certificate of a verified chain against the lists, the server's own first.
    /// </summary>
    /// <param name="chain">The chain built for the server certificate, ending at its root.</param>
    /// <param name="now">The moment the lists' dates are checked against.</param>
    /// <returns>
    /// <see langword="null" /> when no certificate is refused; otherwise the OpenSSL verify
    /// result of the first refusal (<see cref="OpenSslVerifyResult" />).
    /// </returns>
    public long? FirstRefusal(X509Chain chain, DateTimeOffset now)
    {
        var elements = chain.ChainElements;
        for (var depth = 0; depth < elements.Count; depth++)
        {
            var issuer = elements[Math.Min(depth + 1, elements.Count - 1)].Certificate;
            if (Refusal(elements[depth].Certificate, issuer, now) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks one certificate as OpenSSL's <c>check_crl</c> and <c>cert_crl</c> do: a list
    /// from its issuer must be found (the first one valid at <paramref name="now" />, else the
    /// first), the issuer's key usage, if it has one, must allow CRL signing, the list must be
    /// valid at <paramref name="now" /> and signed by the issuer, and must not name the
    /// certificate's serial number.
    /// </summary>
    /// <param name="certificate">The certificate checked.</param>
    /// <param name="issuer">Its issuer, the next certificate of the chain, or itself for the root.</param>
    /// <param name="now">The moment the list's dates are checked against.</param>
    /// <returns><see langword="null" /> to accept, otherwise the OpenSSL verify result.</returns>
    internal long? Refusal(X509Certificate2 certificate, X509Certificate2 issuer, DateTimeOffset now)
    {
        var candidates = _lists.Where(list => list.CoversCertificatesIssuedFor(certificate)).ToList();
        if (candidates.Count == 0)
        {
            return OpenSslVerifyResult.UnableToGetCertificateRevocationList;
        }

        var list = candidates.FirstOrDefault(candidate => DateRefusal(candidate, now) is null) ?? candidates[0];
        if (!AllowsSigningRevocationLists(issuer))
        {
            return OpenSslVerifyResult.KeyUsageDoesNotIncludeCrlSigning;
        }

        return DateRefusal(list, now) ?? SignatureOrRevocationRefusal(list, certificate, issuer);
    }

    private static long? SignatureOrRevocationRefusal(CertificateRevocationList list, X509Certificate2 certificate, X509Certificate2 issuer)
    {
        if (!list.IsSignedBy(issuer))
        {
            return OpenSslVerifyResult.CertificateRevocationListSignatureFailure;
        }

        return list.Revokes(certificate) ? OpenSslVerifyResult.CertificateRevoked : null;
    }

    private static long? DateRefusal(CertificateRevocationList list, DateTimeOffset now)
    {
        if (list.ThisUpdate > now)
        {
            return OpenSslVerifyResult.CertificateRevocationListNotYetValid;
        }

        return list.NextUpdate < now ? OpenSslVerifyResult.CertificateRevocationListHasExpired : null;
    }

    private static bool AllowsSigningRevocationLists(X509Certificate2 issuer) =>
        issuer.Extensions.OfType<X509KeyUsageExtension>().FirstOrDefault() is not { } keyUsage
        || keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.CrlSign);

    private static List<CertificateRevocationList> DecodeAll(string pem)
    {
        var lists = new List<CertificateRevocationList>();
        var rest = pem.AsMemory();
        while (PemEncoding.TryFind(rest.Span, out var fields))
        {
            if (rest.Span[fields.Label].SequenceEqual(PemLabel))
            {
                lists.Add(CertificateRevocationList.Decode(Convert.FromBase64String(rest.Span[fields.Base64Data].ToString())));
            }

            rest = rest[fields.Location.End..];
        }

        return lists.Count == 0
            ? throw new CryptographicException("The file holds no certificate revocation list.")
            : lists;
    }
}
