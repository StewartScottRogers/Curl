using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Revocation of the certificates below a non-self-signed <c>--cacert</c> or <c>--proxy-cacert</c>
/// anchor, which the Windows chain engine cannot answer (BL-5384).
/// </summary>
internal sealed partial class ServerCertificateVerification
{
    private const string CrlDistributionPointsOid = "2.5.29.31";

    private const string FreshestCrlOid = "2.5.29.46";

    private static readonly Asn1Tag ContextTag0 = new(TagClass.ContextSpecific, 0);
    private static readonly Asn1Tag ContextTag1 = new(TagClass.ContextSpecific, 1);

    private static readonly Asn1Tag ContextTag6 = new(TagClass.ContextSpecific, 6);

    /// <summary>
    /// Downloads a certificate revocation list from the address a certificate's CRL distribution
    /// point names, or throws (an <see cref="IOException" />, <see cref="HttpRequestException" />,
    /// <see cref="OperationCanceledException" />, <see cref="InvalidOperationException" />,
    /// <see cref="UriFormatException" /> or <see cref="ArgumentException" />, its message the reason)
    /// when it cannot be had. It must stop when the token is cancelled. A test seam.
    /// </summary>
    internal Func<Uri, CancellationToken, byte[]> FetchRevocationList { get; init; } = RevocationListDownload.Fetch;

    /// <summary>
    /// The most time all the list and OCSP response fetches of one verification may take together; what is left of
    /// it bounds each fetch, and a fetch past it counts as unfetchable. Five seconds unless set.
    /// </summary>
    internal TimeSpan RevocationFetchBudget { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Where a failed list or OCSP response fetch is written, at <c>warning</c> as <c>tls</c>.</summary>
    internal IDiagnosticLog RevocationDiagnosticLog { get; init; } = NoDiagnosticLog.Instance;

    /// <summary>
    /// Ends every list and OCSP response fetch of the verification when it is cancelled; the connect's limit, so a
    /// stalled fetch does not outlast what is left of the connect (ADR-0117).
    /// </summary>
    internal CancellationToken RevocationLimit { get; init; }

    /// <summary>
    /// Returns a verification with this one's settings and seams whose below-anchor revocation fetches
    /// write their failures to <paramref name="diagnosticLog" /> and stop at
    /// <paramref name="limit" />, and are bounded by the lesser of this one's budget and what is
    /// left of the connect, which <paramref name="limit" /> cancels at.
    /// </summary>
    /// <param name="diagnosticLog">The connect target's diagnostic log.</param>
    /// <param name="limit">Cancelled when the connect's limit passes.</param>
    /// <param name="isProxy">Whether the handshake is with an HTTPS proxy (BL-5828).</param>
    /// <returns>A verification for one handshake.</returns>
    internal ServerCertificateVerification ForHandshake(IDiagnosticLog diagnosticLog, CancellationToken limit, bool isProxy = false) =>
        new(options, matchesSchannelBuild, timeProvider)
        {
            IsProxy = isProxy,
            FetchRevocationList = FetchRevocationList,
            FetchOcspResponse = FetchOcspResponse,
            RevocationFetchBudget = RevocationFetchBudget,
            RevocationDiagnosticLog = diagnosticLog,
            RevocationLimit = limit,
            ReadChainStatus = ReadChainStatus,
            ReadCertificatesBelowAnchorStatus = ReadCertificatesBelowAnchorStatus,
            ReadEnvironmentVariable = ReadEnvironmentVariable,
            DefaultCaBundleCandidates = DefaultCaBundleCandidates,
        };

    /// <summary>
    /// Returns a verification with this one's settings and seams for a handshake with an HTTPS
    /// proxy, whose <see cref="IsProxy" /> is set (BL-5828).
    /// </summary>
    /// <returns>A verification for the proxy's handshake.</returns>
    internal ServerCertificateVerification ForProxy() => ForHandshake(RevocationDiagnosticLog, RevocationLimit, isProxy: true);

    private readonly ConditionalWeakTable<X509Chain, BelowAnchorRevocationAnswer> _belowAnchorAnswers = new();

    private sealed record BelowAnchorRevocationAnswer(BelowAnchorRevocation Value);

    /// <summary>What OCSP and the CRLs say of the certificates below an anchor.</summary>
    private enum BelowAnchorRevocation
    {
        /// <summary>Nothing is unknown or revoked.</summary>
        Clean,

        /// <summary>A certificate below the anchor is revoked, by OCSP or a list.</summary>
        Revoked,

        /// <summary>A revocation status stays unknown: no usable OCSP answer or list was found.</summary>
        Unresolved,
    }

    // With a chain ending at a non-self-signed anchor the Windows engine reports every element's
    // revocation status unknown, even with a valid answer one fetch away, so it is read here:
    // each certificate below the anchor is asked about by OCSP, else looked up in the list its issuer signed.
    // Worked out once per chain, so once per verification, however many checks ask.
    private BelowAnchorRevocation ReadBelowAnchorRevocation(X509Chain chain) =>
        _belowAnchorAnswers.GetValue(chain, key => new BelowAnchorRevocationAnswer(WorkOutBelowAnchorRevocation(key))).Value;

    private BelowAnchorRevocation WorkOutBelowAnchorRevocation(X509Chain chain)
    {
        if (ReadCertificatesBelowAnchorStatus(chain).All(status => (status.Status & UnavailableRevocationFlags) == 0))
        {
            return BelowAnchorRevocation.Clean;
        }

        var elements = chain.ChainElements.Cast<X509ChainElement>().Select(element => element.Certificate).Take(AnchorIndexOf(chain) + 1).ToArray();
        using var budget = new CancellationTokenSource(RevocationFetchBudget, timeProvider);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, RevocationLimit);
        var outcomes = elements.Take(elements.Length - 1).Select((certificate, index) => RevocationOf(certificate, elements[index + 1], limited.Token)).ToArray();
        return outcomes.Contains(BelowAnchorRevocation.Revoked)
            ? BelowAnchorRevocation.Revoked
            : outcomes.Contains(BelowAnchorRevocation.Unresolved) ? BelowAnchorRevocation.Unresolved : BelowAnchorRevocation.Clean;
    }

    private BelowAnchorRevocation RevocationOf(X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget) =>
        OcspRevocationOf(certificate, issuer, budget) ?? ListRevocationOf(certificate, issuer, budget);

    private BelowAnchorRevocation ListRevocationOf(X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        // CryptoAPI stops at the first usable list (STD-163): a later point is never asked, so its answer cannot overrule it.
        var first = CrlDistributionPointUris(certificate)
            .Select(uri => UsableRevocationList(uri, certificate, issuer, budget))
            .OfType<CertificateRevocationList>()
            .FirstOrDefault();
        return first is null
            ? BelowAnchorRevocation.Unresolved
            : first.Revokes(certificate) ? RevokedUnlessDeltaReleases(first, certificate, issuer, budget) : DeltaRevocationOf(first, certificate, issuer, budget);
    }

    // The delta lists a base list or the certificate points to through freshestCRL (RFC 5280, 5.2.6): each
    // pointer needs a usable delta list, and a pointer with none (unfetchable, unusable, or not http) leaves the status unknown.
    private BelowAnchorRevocation DeltaRevocationOf(CertificateRevocationList baseList, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        var pointers = FreshestPointers(baseList, certificate);
        var outcomes = pointers.Select(pointer => DeltaRevocationOf(pointer, baseList, certificate, issuer, budget)).ToArray();
        return outcomes.Contains(BelowAnchorRevocation.Revoked)
            ? BelowAnchorRevocation.Revoked
            : outcomes.Contains(BelowAnchorRevocation.Unresolved) ? BelowAnchorRevocation.Unresolved : BelowAnchorRevocation.Clean;
    }

    // A hold in the base list is lifted by a usable delta entry with reason removeFromCRL (RFC 5280, 5.3.1; STD-188).
    private BelowAnchorRevocation RevokedUnlessDeltaReleases(CertificateRevocationList baseList, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        return FreshestPointers(baseList, certificate).SelectMany(pointer => UsableDeltas(pointer, baseList, certificate, issuer, budget)).Any(delta => delta.Releases(certificate))
            ? BelowAnchorRevocation.Clean
            : BelowAnchorRevocation.Revoked;
    }

    private static IEnumerable<byte[]> FreshestPointers(CertificateRevocationList baseList, X509Certificate2 certificate) =>
        new[] { baseList.FreshestCrlValue, certificate.Extensions[FreshestCrlOid]?.RawData }.OfType<byte[]>();

    // A delta that is expired or newer than the base list is not usable, yet CryptoAPI still reports the leaf revoked when it lists it (STD-189).
    private BelowAnchorRevocation DeltaRevocationOf(byte[] pointer, CertificateRevocationList baseList, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        var candidates = CandidateDeltas(pointer, certificate, issuer, budget);
        var deltas = candidates.Where(delta => IsCurrentDeltaOf(delta, baseList, certificate)).ToArray();
        return candidates.Any(delta => delta.Revokes(certificate))
            ? BelowAnchorRevocation.Revoked
            : deltas.Length == 0 ? BelowAnchorRevocation.Unresolved : BelowAnchorRevocation.Clean;
    }

    private CertificateRevocationList[] UsableDeltas(byte[] pointer, CertificateRevocationList baseList, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget) =>
        CandidateDeltas(pointer, certificate, issuer, budget).Where(delta => IsCurrentDeltaOf(delta, baseList, certificate)).ToArray();

    private bool IsCurrentDeltaOf(CertificateRevocationList delta, CertificateRevocationList baseList, X509Certificate2 certificate) =>
        delta.IsUnexpiredAt(timeProvider.GetUtcNow()) && delta.IsDeltaOf(baseList, certificate);

    private CertificateRevocationList[] CandidateDeltas(byte[] pointer, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        return DistributionPointUris(pointer)
            .Select(uri => TryFetchRevocationList(uri, budget))
            .Select(der => der is null ? null : DecodeOrNothing(der))
            .OfType<CertificateRevocationList>()
            .Where(delta => delta.CoversCertificatesIssuedFor(certificate) && delta.IsSignedBy(issuer))
            .ToArray();
    }

    // A list is usable when it decodes, is the issuer's, is signed by the issuer, has not expired and covers the certificate.
    private CertificateRevocationList? UsableRevocationList(Uri uri, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        var der = TryFetchRevocationList(uri, budget);
        var list = der is null ? null : DecodeOrNothing(der);
        return list is not null && IsCurrentListOf(list, certificate, issuer, uri) ? list : null;
    }

    private byte[]? TryFetchRevocationList(Uri uri, CancellationToken budget) => TryFetch(FetchRevocationList, "CRL", uri, budget);

    private byte[]? TryFetch(Func<Uri, CancellationToken, byte[]> fetch, string kind, Uri uri, CancellationToken budget)
    {
        try
        {
            budget.ThrowIfCancellationRequested();
            return fetch(uri, budget);
        }
        // UriFormatException and ArgumentException: HttpClient follows a redirect to a file or opaque address and fails opening it (STD-205).
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException or UriFormatException or ArgumentException)
        {
            if (RevocationDiagnosticLog.IsEnabled(DiagnosticLogLevel.Warning))
            {
                RevocationDiagnosticLog.Write(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Tls, $"{kind} {uri} could not be fetched: {exception.Message}");
            }

            return null;
        }
    }

    private bool IsCurrentListOf(CertificateRevocationList list, X509Certificate2 certificate, X509Certificate2 issuer, Uri uri) =>
        list.CoversCertificatesIssuedFor(certificate) && list.IsSignedBy(issuer) && list.IsUnexpiredAt(timeProvider.GetUtcNow()) && list.CoversScopeOf(certificate, uri);

    // A list served as DER or as PEM text (curl accepts both); PEM armour is stripped before decoding.
    internal static CertificateRevocationList? DecodeOrNothing(byte[] body)
    {
        try
        {
            return CertificateRevocationList.Decode(DerOf(body));
        }
        catch (Exception exception) when (exception is AsnContentException or FormatException)
        {
            return null;
        }
    }

    private static byte[] DerOf(byte[] body)
    {
        var text = System.Text.Encoding.ASCII.GetString(body);
        return PemEncoding.TryFind(text, out var fields) && text[fields.Label] == "X509 CRL"
            ? Convert.FromBase64String(text[fields.Base64Data])
            : body;
    }

    // The http addresses in a certificate's CRL distribution points (RFC 5280, 4.2.1.13); none
    // for a certificate without the extension or with one that does not parse.
    internal static IReadOnlyList<Uri> CrlDistributionPointUris(X509Certificate2 certificate)
    {
        return certificate.Extensions[CrlDistributionPointsOid] is { } extension ? DistributionPointUris(extension.RawData) : [];
    }

    // The http addresses of a CRLDistributionPoints value, as in a cRLDistributionPoints or freshestCRL extension.
    private static IReadOnlyList<Uri> DistributionPointUris(byte[] value)
    {
        var uris = new List<Uri>();
        try
        {
            var points = new AsnReader(value, AsnEncodingRules.DER).ReadSequence();
            while (points.HasData)
            {
                AddUrisOfPoint(points.ReadSequence(), uris);
            }
        }
        catch (AsnContentException)
        {
            return [];
        }

        return uris;
    }

    // DistributionPoint ::= SEQUENCE { distributionPoint [0] { fullName [0] GeneralNames }, reasons [1], ... }
    // A point limited to some reasons holds only a partial list, which proves nothing about the
    // other reasons, so it yields no address and the status stays unknown, as CryptoAPI reports it.
    private static void AddUrisOfPoint(AsnReader point, List<Uri> uris)
    {
        var pointUris = new List<Uri>();
        var limitedToReasons = false;
        while (point.HasData)
        {
            if (!point.PeekTag().HasSameClassAndValue(ContextTag0))
            {
                limitedToReasons |= point.PeekTag().HasSameClassAndValue(ContextTag1);
                point.ReadEncodedValue();
                continue;
            }

            var name = point.ReadSequence(ContextTag0);
            while (name.HasData)
            {
                AddUrisOfFullName(name, pointUris);
            }
        }

        if (!limitedToReasons)
        {
            uris.AddRange(pointUris);
        }
    }

    private static void AddUrisOfFullName(AsnReader name, List<Uri> uris)
    {
        if (!name.PeekTag().HasSameClassAndValue(ContextTag0))
        {
            name.ReadEncodedValue();
            return;
        }

        var generalNames = name.ReadSequence(ContextTag0);
        while (generalNames.HasData)
        {
            if (ReadHttpAddress(generalNames) is { } uri)
            {
                uris.Add(uri);
            }
        }
    }

    // One GeneralName: its address when it is an http uniformResourceIdentifier, else nothing.
    private static Uri? ReadHttpAddress(AsnReader generalNames)
    {
        if (!generalNames.PeekTag().HasSameClassAndValue(ContextTag6))
        {
            generalNames.ReadEncodedValue();
            return null;
        }

        var text = generalNames.ReadCharacterString(UniversalTagNumber.IA5String, ContextTag6);
        return HttpAddressOrNothing(text);
    }

    private static Uri? HttpAddressOrNothing(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp ? uri : null;
}
