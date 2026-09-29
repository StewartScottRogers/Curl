namespace Curl.Tls;

/// <summary>The verdict on a stapled OCSP response, as <c>--cert-status</c> reports it (exit 91 unless <see cref="IsGood" />).</summary>
/// <param name="Status">What the check found.</param>
/// <param name="Code">
/// The CRL reason of a <see cref="OcspStapleStatus.Revoked" /> certificate (-1 when none
/// was given), the <c>responseStatus</c> of an <see cref="OcspStapleStatus.Unsuccessful" />
/// response, otherwise 0.
/// </param>
public sealed record OcspStapleOutcome(OcspStapleStatus Status, int Code = 0)
{
    /// <summary>Gets a value indicating whether the response vouches for the certificate.</summary>
    public bool IsGood => Status == OcspStapleStatus.Good;
}
