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
public sealed class HandBuiltSecurityContextFactory(KerberosServiceTicketSource tickets, TimeProvider timeProvider, IKerberosRandomSource randomSource, INtlmRandomSource ntlmRandomSource) : ISecurityContextFactory
{
    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Mechanism == SecurityMechanism.Ntlm
            ? new HandBuiltNtlmSecurityContext(request, new NtlmChallengeAnswerer(timeProvider, ntlmRandomSource))
            : new HandBuiltKerberosSecurityContext(request, tickets, timeProvider, randomSource);
    }
}
