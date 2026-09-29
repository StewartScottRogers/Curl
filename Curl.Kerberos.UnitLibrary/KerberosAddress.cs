namespace Curl.Kerberos;

/// <summary>A host address a ticket is bound to (RFC 4120's <c>HostAddress</c>).</summary>
/// <param name="AddressType">The address type, e.g. 2 for IPv4, 24 for IPv6.</param>
/// <param name="Address">The address bytes.</param>
public sealed record KerberosAddress(int AddressType, byte[] Address);
