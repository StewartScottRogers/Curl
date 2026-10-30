using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Tlsa;

/// <summary>
/// The <c>Tlsa/</c> folder's matcher: decides whether a server's certificate chain matches a TLSA record set
/// (RFC 6698 section 2.1, RFC 7671 sections 5.1 to 5.4). It runs after Curl's own PKIX verification has accepted the
/// chain (ADR-1016), so it checks neither names nor expiry (RFC 7671 section 5.1) nor signatures: it compares each
/// usable record with the certificates its usage names, in the chain as the server sent it.
/// </summary>
public static class TlsaCertificateMatcher
{
    private const byte PkixEndEntity = 1;
    private const byte DaneEndEntity = 3;
    private const byte FullCertificate = 0;
    private const byte SubjectPublicKeyInfo = 1;
    private const byte ExactMatch = 0;
    private const byte Sha256Match = 1;
    private const byte Sha512Match = 2;

    /// <summary>
    /// Compares <paramref name="chain" /> with <paramref name="records" />. PKIX-EE(1) and DANE-EE(3) records match
    /// the server's own certificate; PKIX-TA(0) and DANE-TA(2) records match any certificate after it. Selector 0
    /// takes the whole certificate and 1 its SubjectPublicKeyInfo; matching type 0 compares it exactly, 1 its SHA-256
    /// and 2 its SHA-512. Records with any other usage, selector or matching type are unusable and ignored.
    /// </summary>
    /// <param name="chain">The certificates as DER, the server's own first, each parseable as an X.509 certificate.</param>
    /// <param name="records">The TLSA records to match.</param>
    /// <returns><see cref="TlsaMatchResult.NoUsableRecords" /> when no record is usable, otherwise whether one matched.</returns>
    public static TlsaMatchResult Match(IReadOnlyList<byte[]> chain, IEnumerable<TlsaRecordData> records)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(records);
        List<TlsaRecordData> usable = [.. records.Where(IsUsable)];
        if (usable.Count == 0)
        {
            return TlsaMatchResult.NoUsableRecords;
        }

        return usable.Any(record => MatchesChain(chain, record)) ? TlsaMatchResult.Match : TlsaMatchResult.NoMatch;
    }

    private static bool IsUsable(TlsaRecordData record) =>
        record.CertificateUsage <= DaneEndEntity
        && record.Selector <= SubjectPublicKeyInfo
        && record.MatchingType <= Sha512Match;

    private static bool MatchesChain(IReadOnlyList<byte[]> chain, TlsaRecordData record) =>
        CandidatesFor(chain, record.CertificateUsage).Any(certificate => MatchesCertificate(certificate, record));

    private static IEnumerable<byte[]> CandidatesFor(IReadOnlyList<byte[]> chain, byte usage) =>
        usage is PkixEndEntity or DaneEndEntity ? chain.Take(1) : chain.Skip(1);

    private static bool MatchesCertificate(byte[] certificate, TlsaRecordData record) =>
        Digest(Select(certificate, record.Selector), record.MatchingType)
            .AsSpan()
            .SequenceEqual(record.CertificateAssociationData);

    private static byte[] Select(byte[] certificate, byte selector) =>
        selector == FullCertificate ? certificate : SubjectPublicKeyInfoOf(certificate);

    private static byte[] SubjectPublicKeyInfoOf(byte[] certificate)
    {
        using X509Certificate2 parsed = X509CertificateLoader.LoadCertificate(certificate);
        return parsed.PublicKey.ExportSubjectPublicKeyInfo();
    }

    private static byte[] Digest(byte[] selected, byte matchingType) => matchingType switch
    {
        ExactMatch => selected,
        Sha256Match => SHA256.HashData(selected),
        _ => SHA512.HashData(selected),
    };
}
