namespace Curl.Protocol.Abstractions;

/// <summary>The GSS-API style mechanism an <see cref="ISecurityContext" /> runs (ADR-0142).</summary>
public enum SecurityMechanism
{
    /// <summary>NTLM: <c>--ntlm</c>, <c>--proxy-ntlm</c>, SASL <c>NTLM</c>.</summary>
    Ntlm,

    /// <summary>SPNEGO (RFC 4178) choosing Kerberos, or NTLM where the platform offers it: <c>--negotiate</c>, <c>--proxy-negotiate</c>.</summary>
    Negotiate,

    /// <summary>The GSS-API Kerberos V5 mechanism (RFC 4121) alone: SASL <c>GSSAPI</c>, SOCKS5 GSS-API.</summary>
    Kerberos,
}
