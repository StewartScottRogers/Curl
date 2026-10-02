using Curl.Kerberos;
using Curl.Ntlm;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Makes contexts on the hand-built route of ADR-0142: curl's own NTLM
/// (<see cref="HandBuiltNtlmSecurityContext" />) for <see cref="SecurityMechanism.Ntlm" />,
/// SPNEGO over the hand-built Kerberos for <see cref="SecurityMechanism.Negotiate" />, and the
/// hand-built Kerberos alone for <see cref="SecurityMechanism.Kerberos" />.
/// </summary>
/// <param name="tickets">Gets service tickets from the credential cache and the KDC.</param>
/// <param name="timeProvider">Gives authenticator times and the NTLMv2 timestamp.</param>
/// <param name="randomSource">Gives subkeys, sequence numbers and confounders.</param>
/// <param name="ntlmRandomSource">Gives the NTLMv2 client challenge.</param>
/// <param name="diagnosticLog">Where the context chosen for each request is logged at <c>verbose</c> (BL-1151); <see langword="null" /> logs nothing.</param>
public sealed class HandBuiltSecurityContextFactory(KerberosServiceTicketSource tickets, TimeProvider timeProvider, IKerberosRandomSource randomSource, INtlmRandomSource ntlmRandomSource, IDiagnosticLog? diagnosticLog = null) : ISecurityContextFactory
{
    private readonly AuthDiagnosticLog log = new(diagnosticLog);

    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Mechanism == SecurityMechanism.Ntlm)
        {
            log.SecurityContextChosen(request, "hand-built NTLM", "NTLM asked for");
            return new HandBuiltNtlmSecurityContext(request, new NtlmChallengeAnswerer(timeProvider, ntlmRandomSource));
        }

        log.SecurityContextChosen(request, "hand-built Kerberos", KerberosWhy(request.Mechanism));
        return new HandBuiltKerberosSecurityContext(request, tickets, timeProvider, randomSource);
    }

    /// <summary>
    /// Gets why the hand-built Kerberos answers <paramref name="mechanism" />: inside SPNEGO for
    /// Negotiate, which offers Kerberos V5 alone and never NTLM, or on its own for Kerberos.
    /// </summary>
    private static string KerberosWhy(SecurityMechanism mechanism) =>
        mechanism == SecurityMechanism.Negotiate
            ? "SPNEGO offering Kerberos V5 alone, not NTLM"
            : "Kerberos asked for";
}
