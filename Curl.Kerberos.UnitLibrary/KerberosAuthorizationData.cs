namespace Curl.Kerberos;

/// <summary>One element of a ticket's authorization data (RFC 4120's <c>AuthorizationData</c>).</summary>
/// <param name="DataType">The <c>ad-type</c>.</param>
/// <param name="Data">The <c>ad-data</c> bytes.</param>
public sealed record KerberosAuthorizationData(int DataType, byte[] Data);
