using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Makes NTLM contexts with the hand-built factory and every other context with the router, so
/// a run answers NTLM as curl's non-SSPI build does - its own type-1, type-2 and type-3 messages -
/// on Windows too, where ADR-0142's router sends NTLM to SSPI (BL-1858). The upstream case harness
/// runs Curl as the non-SSPI build because its platform feature list (<c>UpstreamCurlPlatform</c>)
/// has no <c>SSPI</c>, so upstream's <c>!SSPI</c> NTLM cases run and expect curl's hand-built
/// messages, even though Curl's own <c>curl -V</c> lists <c>SSPI</c> on Windows (ADR-0439).
/// </summary>
/// <param name="router">Makes the Negotiate and Kerberos contexts: ADR-0142's router.</param>
/// <param name="handBuilt">Makes the NTLM contexts: the hand-built factory.</param>
internal sealed class HandBuiltNtlmSecurityContextFactory(ISecurityContextFactory router, ISecurityContextFactory handBuilt) : ISecurityContextFactory
{
    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Mechanism == SecurityMechanism.Ntlm ? handBuilt.Create(request) : router.Create(request);
    }
}
