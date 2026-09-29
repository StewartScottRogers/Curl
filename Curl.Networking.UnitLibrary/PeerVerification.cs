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
}
