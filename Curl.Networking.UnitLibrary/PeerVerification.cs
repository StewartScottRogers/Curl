using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// What verification learned about the server's certificate, for the handshake event:
/// whether it verified, OpenSSL's code for it, and the DER of the chain the event reports.
/// </summary>
/// <param name="Verified">Whether the certificate was accepted, <c>-k</c> aside.</param>
/// <param name="VerifyResult">OpenSSL's <c>X509_V_</c> code, as <see cref="OpenSslVerifyResult" /> maps it.</param>
/// <param name="Chain">The verified chain when the chain verified, else what the server sent.</param>
internal sealed record PeerVerification(bool Verified, long? VerifyResult, ReadOnlyMemory<byte>[] Chain)
{
    /// <summary>Gets the observation of a handshake that never reached its certificate.</summary>
    internal static PeerVerification Unobserved { get; } = new(false, null, []);

    /// <summary>
    /// Reports <see cref="VerifyResult" /> through
    /// <see cref="ITransferEvents.ReportCertificateVerifyResult" />, the source of
    /// <c>%{ssl_verify_result}</c> and <c>%{proxy_ssl_verify_result}</c>, whether the handshake
    /// went on or not; a code with no known mapping is reported as
    /// <see cref="OpenSslVerifyResult.Unspecified" />. Nothing is reported for
    /// <see cref="Unobserved" />, nor in the Schannel build, whose curl prints <c>0</c> for both
    /// whatever it found (BL-661).
    /// </summary>
    /// <param name="events">Where the code goes.</param>
    /// <param name="isProxy">Whether the handshake was with an HTTPS proxy.</param>
    /// <param name="matchesSchannelBuild">Whether the provider behaves as curl's Schannel build.</param>
    internal void ReportVerifyResult(ITransferEvents events, bool isProxy, bool matchesSchannelBuild)
    {
        if (matchesSchannelBuild || ReferenceEquals(this, Unobserved))
        {
            return;
        }

        events.ReportCertificateVerifyResult(VerifyResult ?? OpenSslVerifyResult.Unspecified, isProxy);
    }
}
