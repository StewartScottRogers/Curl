using Curl.Tls;

namespace Curl.Quic;

/// <summary>A certificate verifier that accepts every chain, or rejects every chain with <see cref="Rejection" />.</summary>
internal sealed class QuicTestVerifier : IServerCertificateVerifier
{
    /// <summary>Gets the reason every chain is rejected with, or <see langword="null" /> to accept every chain.</summary>
    public string? Rejection { get; init; }

    /// <summary>Gets the chain last presented.</summary>
    public ServerCertificateChain? Presented { get; private set; }

    public ServerCertificateVerdict Verify(ServerCertificateChain presented)
    {
        Presented = presented;
        return Rejection is null ? ServerCertificateVerdict.Accepted : ServerCertificateVerdict.Rejected(Rejection);
    }
}
