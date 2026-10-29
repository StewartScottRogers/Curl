using Curl.Protocol.Abstractions;

namespace Curl.Extension.Coap.Dtls.Handshake;

/// <summary>
/// A TLS provider that only verifies chains, as curl's trust settings would for a self-signed server
/// certificate: it rejects every chain with exit 60, unless <paramref name="insecure" /> (<c>-k</c>)
/// accepts them all. It records the chain and host it was asked about.
/// </summary>
internal sealed class TrustSettingTlsProvider(bool insecure) : ITlsProvider
{
    /// <summary>Gets the certificates of the last chain verified.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> VerifiedCertificates { get; private set; } = [];

    /// <summary>Gets the host the last chain was verified for.</summary>
    public string? VerifiedHost { get; private set; }

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The DTLS handshake only asks the provider to verify a chain.");

    /// <inheritdoc />
    public ServerCertificateChainVerdict VerifyServerCertificateChain(IReadOnlyList<ReadOnlyMemory<byte>> certificates, string targetHost)
    {
        VerifiedCertificates = certificates;
        VerifiedHost = targetHost;
        return insecure
            ? ServerCertificateChainVerdict.Accepted
            : ServerCertificateChainVerdict.Rejected(CurlExitCode.PeerFailedVerification, "SSL certificate problem: self-signed certificate");
    }
}
