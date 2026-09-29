namespace Curl.Tls;

/// <summary>
/// Verifies the server's certificate chain for the hand-built client (ADR-0140, "The
/// verification hand-off"). The client never builds or checks a chain itself; it hands
/// the chain here and acts on the verdict. <c>Curl.Networking.UnitLibrary</c> implements
/// it with the same code that verifies for <c>SslStream</c>.
/// </summary>
public interface IServerCertificateVerifier
{
    /// <summary>Verifies the chain the server presented.</summary>
    /// <param name="presented">The certificates, the name the client offered and any stapled OCSP response.</param>
    /// <returns>Whether the chain is accepted, and if not, why.</returns>
    ServerCertificateVerdict Verify(ServerCertificateChain presented);
}
