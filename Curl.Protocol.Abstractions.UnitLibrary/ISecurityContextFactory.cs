namespace Curl.Protocol.Abstractions;

/// <summary>
/// Makes the <see cref="ISecurityContext" /> an NTLM, Negotiate or Kerberos exchange runs on
/// (ADR-0142): the seam every caller of those mechanisms takes, so protocol libraries,
/// <c>Curl.Networking.UnitLibrary</c> and <c>Curl.Authentication.UnitLibrary</c> share one
/// contract and tests supply tokens without SSPI, GSS-API or a KDC.
/// </summary>
public interface ISecurityContextFactory
{
    /// <summary>Makes a context for <paramref name="request" />. It does no I/O; the first step does.</summary>
    /// <param name="request">The mechanism, acceptor and credential.</param>
    /// <returns>A new context; the caller disposes it.</returns>
    ISecurityContext Create(SecurityContextRequest request);
}
