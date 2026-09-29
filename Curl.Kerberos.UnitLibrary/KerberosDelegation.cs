namespace Curl.Kerberos;

/// <summary>When the initiator forwards its ticket-granting ticket to the acceptor: curl's <c>--delegation</c> levels.</summary>
public enum KerberosDelegation
{
    /// <summary><c>none</c>: never, curl's default.</summary>
    None,

    /// <summary><c>policy</c>: only when the service ticket carries <see cref="KerberosTicketFlags.OkAsDelegate" /> (MIT's <c>GSS_C_DELEG_POLICY_FLAG</c>).</summary>
    Policy,

    /// <summary><c>always</c>: whenever a forwardable ticket-granting ticket is at hand.</summary>
    Always,
}
