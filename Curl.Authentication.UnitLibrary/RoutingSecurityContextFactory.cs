using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Chooses the implementation of each exchange by ADR-0142's tables: on Windows the system
/// (SSPI) context for every mechanism and credential, Negotiate never falling back to NTLM
/// (<see cref="SspiNegotiateSecurityContext" />, ADR-0176); elsewhere the hand-built route for
/// NTLM, and for Negotiate and Kerberos the system GSS-API context with the default
/// credentials, falling back to the hand-built route only when it answers
/// <see cref="SecurityContextStatus.NoMechanism" />.
/// </summary>
/// <param name="isWindows">Whether the running system is Windows (<see cref="OperatingSystem.IsWindows" />).</param>
/// <param name="system">Makes contexts over <c>NegotiateAuthentication</c>.</param>
/// <param name="handBuilt">Makes contexts over the hand-built libraries.</param>
public sealed class RoutingSecurityContextFactory(bool isWindows, ISecurityContextFactory system, ISecurityContextFactory handBuilt) : ISecurityContextFactory
{
    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (isWindows)
        {
            // curl's SSPI code never reads --delegation: its contexts ask no delegation (ADR-0188).
            ISecurityContext sspi = system.Create(request with { Delegation = SecurityDelegation.None });
            return request.Mechanism == SecurityMechanism.Negotiate ? new SspiNegotiateSecurityContext(sspi) : sspi;
        }

        if (request.Mechanism == SecurityMechanism.Ntlm)
        {
            return handBuilt.Create(request);
        }

        // curl's GSS-API Negotiate ignores -u's user and password and uses the credential cache.
        SecurityContextRequest cached = request with { UserName = null, Password = null, Domain = null };
        return new FallbackSecurityContext(system.Create(cached), () => handBuilt.Create(cached));
    }
}
