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
/// <param name="diagnosticLog">Where each route taken, and why, is logged at <c>verbose</c> (BL-923); <see langword="null" /> logs nothing.</param>
public sealed class RoutingSecurityContextFactory(bool isWindows, ISecurityContextFactory system, ISecurityContextFactory handBuilt, IDiagnosticLog? diagnosticLog = null) : ISecurityContextFactory
{
    private readonly AuthDiagnosticLog log = new(diagnosticLog);

    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (isWindows)
        {
            // curl's SSPI code never reads --delegation: its contexts ask no delegation (ADR-0188).
            log.SecurityContextChosen(request, "system (SSPI)", "Windows, as curl's Schannel build uses SSPI");
            ISecurityContext sspi = system.Create(request with { Delegation = SecurityDelegation.None });
            return request.Mechanism == SecurityMechanism.Negotiate ? new SspiNegotiateSecurityContext(sspi) : sspi;
        }

        if (request.Mechanism == SecurityMechanism.Ntlm)
        {
            log.SecurityContextChosen(request, "hand-built", "curl's own NTLM off Windows");
            return handBuilt.Create(request);
        }

        // curl's GSS-API Negotiate ignores -u's user and password and uses the credential cache.
        SecurityContextRequest cached = request with { UserName = null, Password = null, Domain = null };
        log.SecurityContextChosen(request, "system GSS-API, else hand-built", "off Windows the default credentials, falling back when GSS-API is unsupported");
        return new FallbackSecurityContext(system.Create(cached), () => handBuilt.Create(cached));
    }
}
