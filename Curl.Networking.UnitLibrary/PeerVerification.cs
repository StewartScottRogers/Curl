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
    // curl's " public key hash: sha256//..." keeps its leading space after the "* ".
    private const string PinnedPublicKeyHashLinePrefix = " public key hash: ";

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

    /// <summary>
    /// Gets what the <c>-v</c> line <c> public key hash:</c> names, <c>sha256//</c> and the
    /// server key's hash, when a <c>sha256//</c> <c>--pinnedpubkey</c> was checked; otherwise
    /// <see langword="null" /> (ADR-0336, BL-877).
    /// </summary>
    internal string? PinnedPublicKeyHash { get; init; }

    /// <summary>
    /// Gets a value indicating whether <c>--ssl-revoke-best-effort</c> accepted a chain whose only
    /// faults were an offline or unknown revocation status (BL-968).
    /// </summary>
    internal bool RevocationCheckIncomplete { get; init; }

    /// <summary>Gets a value indicating whether <c>--pinnedpubkey</c> refused the server's key, exit 90.</summary>
    internal bool PinnedPublicKeyRefused { get; init; }

    /// <summary>
    /// Reports the <c>-v</c> lines curl prints when <c>--pinnedpubkey</c> refuses the server's
    /// key, before the exit 90 failure (ADR-0336, BL-877): <c> public key hash:</c> for a
    /// <c>sha256//</c> pin, unless the failed handshake's event already carries it (ADR-0363,
    /// BL-1149), then <c>SSL: public key does not match pinned public key</c>, twice in the
    /// Schannel build (its own line and its error echoed) and once in the OpenSSL build (the
    /// error echoed). Nothing is reported when the pin did not refuse the key.
    /// </summary>
    /// <param name="events">Where the lines go.</param>
    /// <param name="matchesSchannelBuild">Whether the provider behaves as curl's Schannel build.</param>
    /// <param name="failedHandshakeReported">Whether a failed <see cref="TlsHandshakeEvent" /> carrying the hash was reported.</param>
    internal void ReportPinnedPublicKeyRefusal(ITransferEvents events, bool matchesSchannelBuild, bool failedHandshakeReported)
    {
        if (!PinnedPublicKeyRefused)
        {
            return;
        }

        if (!failedHandshakeReported && PinnedPublicKeyHash is { } hash)
        {
            events.ReportInfo(PinnedPublicKeyHashLinePrefix + hash);
        }

        for (var line = matchesSchannelBuild ? 2 : 1; line > 0; line--)
        {
            events.ReportInfo(TlsFailureMessages.PinnedPublicKeyMismatch);
        }
    }
}
