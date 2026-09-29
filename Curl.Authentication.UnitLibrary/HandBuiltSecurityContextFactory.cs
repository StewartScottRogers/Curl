using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// Makes contexts on the hand-built route of ADR-0142: SPNEGO over the hand-built Kerberos for
/// <see cref="SecurityMechanism.Negotiate" />, the hand-built Kerberos alone for
/// <see cref="SecurityMechanism.Kerberos" />. <see cref="SecurityMechanism.Ntlm" /> answers
/// <see cref="SecurityContextStatus.NoMechanism" /> until hand-built NTLM is composed here (BL-526).
/// </summary>
/// <param name="tickets">Gets service tickets from the credential cache and the KDC.</param>
/// <param name="timeProvider">Gives authenticator times.</param>
/// <param name="randomSource">Gives subkeys, sequence numbers and confounders.</param>
public sealed class HandBuiltSecurityContextFactory(KerberosServiceTicketSource tickets, TimeProvider timeProvider, IKerberosRandomSource randomSource) : ISecurityContextFactory
{
    /// <inheritdoc />
    public ISecurityContext Create(SecurityContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Mechanism == SecurityMechanism.Ntlm
            ? new UnavailableSecurityContext()
            : new HandBuiltKerberosSecurityContext(request, tickets, timeProvider, randomSource);
    }
}
