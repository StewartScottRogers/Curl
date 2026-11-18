using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using Curl.Protocol.Abstractions;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Revocation of the certificates below a non-self-signed <c>--cacert</c> or <c>--proxy-cacert</c>
/// anchor, which the Windows chain engine cannot answer (BL-5384).
/// </summary>
internal sealed partial class ServerCertificateVerification
{
    private const string CrlDistributionPointsOid = "2.5.29.31";

    private static readonly Asn1Tag ContextTag0 = new(TagClass.ContextSpecific, 0);

    private static readonly Asn1Tag ContextTag6 = new(TagClass.ContextSpecific, 6);

    /// <summary>
    /// Downloads a certificate revocation list from the address a certificate's CRL distribution
    /// point names, or throws (an <see cref="IOException" />, <see cref="HttpRequestException" />,
    /// <see cref="OperationCanceledException" /> or <see cref="InvalidOperationException" />, its
    /// message the reason) when it cannot be had. It must stop when the token is cancelled. A test seam.
    /// </summary>
    internal Func<Uri, CancellationToken, byte[]> FetchRevocationList { get; init; } = RevocationListDownload.Fetch;

    /// <summary>
    /// The most time all the list fetches of one verification may take together; what is left of
    /// it bounds each fetch, and a fetch past it counts as unfetchable. Five seconds unless set.
    /// </summary>
    internal TimeSpan RevocationFetchBudget { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Where a failed list fetch is written, at <c>warning</c> as <c>tls</c>.</summary>
    internal IDiagnosticLog RevocationDiagnosticLog { get; init; } = NoDiagnosticLog.Instance;

    /// <summary>
    /// Ends every list fetch of the verification when it is cancelled; the connect's limit, so a
    /// stalled fetch does not outlast what is left of the connect (ADR-0117).
    /// </summary>
    internal CancellationToken RevocationLimit { get; init; }

    /// <summary>
    /// Returns a verification with this one's settings and seams whose below-anchor list fetches
    /// write their failures to <paramref name="diagnosticLog" /> and stop at
    /// <paramref name="limit" />, and are bounded by the lesser of this one's budget and what is
    /// left of the connect, which <paramref name="limit" /> cancels at.
    /// </summary>
    /// <param name="diagnosticLog">The connect target's diagnostic log.</param>
    /// <param name="limit">Cancelled when the connect's limit passes.</param>
    /// <returns>A verification for one handshake.</returns>
    internal ServerCertificateVerification ForHandshake(IDiagnosticLog diagnosticLog, CancellationToken limit) =>
        new(options, matchesSchannelBuild, timeProvider)
        {
            FetchRevocationList = FetchRevocationList,
            RevocationFetchBudget = RevocationFetchBudget,
            RevocationDiagnosticLog = diagnosticLog,
            RevocationLimit = limit,
            ReadChainStatus = ReadChainStatus,
            ReadCertificatesBelowAnchorStatus = ReadCertificatesBelowAnchorStatus,
            ReadEnvironmentVariable = ReadEnvironmentVariable,
            DefaultCaBundleCandidates = DefaultCaBundleCandidates,
        };

    private readonly ConditionalWeakTable<X509Chain, BelowAnchorRevocationAnswer> _belowAnchorAnswers = new();

    private sealed record BelowAnchorRevocationAnswer(BelowAnchorRevocation Value);

    /// <summary>What the CRLs say of the certificates below an anchor.</summary>
    private enum BelowAnchorRevocation
    {
        /// <summary>Nothing is unknown or revoked.</summary>
        Clean,

        /// <summary>A certificate below the anchor is listed as revoked.</summary>
        Revoked,

        /// <summary>A revocation status stays unknown: no usable list was found.</summary>
        Unresolved,
    }

    // With a chain ending at a non-self-signed anchor the Windows engine reports every element's
    // revocation status unknown, even with a valid CRL one fetch away, so the lists are read
    // here: the certificates below the anchor are looked up in the list their issuer signed.
    // Worked out once per chain, so once per verification, however many checks ask.
    private BelowAnchorRevocation ReadBelowAnchorRevocation(X509Chain chain) =>
        _belowAnchorAnswers.GetValue(chain, key => new BelowAnchorRevocationAnswer(WorkOutBelowAnchorRevocation(key))).Value;

    private BelowAnchorRevocation WorkOutBelowAnchorRevocation(X509Chain chain)
    {
        if (ReadCertificatesBelowAnchorStatus(chain).All(status => (status.Status & UnavailableRevocationFlags) == 0))
        {
            return BelowAnchorRevocation.Clean;
        }

        var elements = chain.ChainElements.Cast<X509ChainElement>().Select(element => element.Certificate).ToArray();
        using var budget = new CancellationTokenSource(RevocationFetchBudget, timeProvider);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, RevocationLimit);
        var outcomes = elements.Take(elements.Length - 1).Select((certificate, index) => RevocationOf(certificate, elements[index + 1], limited.Token)).ToArray();
        return outcomes.Contains(BelowAnchorRevocation.Revoked)
            ? BelowAnchorRevocation.Revoked
            : outcomes.Contains(BelowAnchorRevocation.Unresolved) ? BelowAnchorRevocation.Unresolved : BelowAnchorRevocation.Clean;
    }

    private BelowAnchorRevocation RevocationOf(X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        var lists = CrlDistributionPointUris(certificate).Select(uri => UsableRevocationList(uri, certificate, issuer, budget)).OfType<CertificateRevocationList>().ToArray();
        return lists.Length == 0
            ? BelowAnchorRevocation.Unresolved
            : lists.Any(list => list.Revokes(certificate)) ? BelowAnchorRevocation.Revoked : BelowAnchorRevocation.Clean;
    }

    // A list is usable when it decodes, is the issuer's, is signed by the issuer and names a next update not yet passed (one naming none fails closed).
    private CertificateRevocationList? UsableRevocationList(Uri uri, X509Certificate2 certificate, X509Certificate2 issuer, CancellationToken budget)
    {
        var der = TryFetchRevocationList(uri, budget);
        var list = der is null ? null : DecodeOrNothing(der);
        return list is not null && IsCurrentListOf(list, certificate, issuer) ? list : null;
    }

    private byte[]? TryFetchRevocationList(Uri uri, CancellationToken budget)
    {
        try
        {
            budget.ThrowIfCancellationRequested();
            return FetchRevocationList(uri, budget);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException)
        {
            if (RevocationDiagnosticLog.IsEnabled(DiagnosticLogLevel.Warning))
            {
                RevocationDiagnosticLog.Write(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Tls, $"CRL {uri} could not be fetched: {exception.Message}");
            }

            return null;
        }
    }

    private bool IsCurrentListOf(CertificateRevocationList list, X509Certificate2 certificate, X509Certificate2 issuer) =>
        list.CoversCertificatesIssuedFor(certificate) && list.IsSignedBy(issuer) && list.NextUpdate >= timeProvider.GetUtcNow();

    private static CertificateRevocationList? DecodeOrNothing(byte[] der)
    {
        try
        {
            return CertificateRevocationList.Decode(der);
        }
        catch (AsnContentException)
        {
            return null;
        }
    }

    // The http addresses in a certificate's CRL distribution points (RFC 5280, 4.2.1.13); none
    // for a certificate without the extension or with one that does not parse.
    internal static IReadOnlyList<Uri> CrlDistributionPointUris(X509Certificate2 certificate)
    {
        if (certificate.Extensions[CrlDistributionPointsOid] is not { } extension)
        {
            return [];
        }

        var uris = new List<Uri>();
        try
        {
            var points = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
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

    // DistributionPoint ::= SEQUENCE { distributionPoint [0] { fullName [0] GeneralNames }, ... }
    private static void AddUrisOfPoint(AsnReader point, List<Uri> uris)
    {
        while (point.HasData)
        {
            if (!point.PeekTag().HasSameClassAndValue(ContextTag0))
            {
                point.ReadEncodedValue();
                continue;
            }

            var name = point.ReadSequence(ContextTag0);
            while (name.HasData)
            {
                AddUrisOfFullName(name, uris);
            }
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
        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttp ? uri : null;
    }
}
