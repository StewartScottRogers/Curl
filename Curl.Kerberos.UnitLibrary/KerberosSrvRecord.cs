namespace Curl.Kerberos;

/// <summary>One DNS SRV record (RFC 2782).</summary>
/// <param name="Priority">Lower is tried first.</param>
/// <param name="Weight">Among records of one priority, higher is tried first.</param>
/// <param name="Port">The service's port.</param>
/// <param name="Target">The host, with or without its trailing dot; <c>.</c> means no service.</param>
public sealed record KerberosSrvRecord(ushort Priority, ushort Weight, ushort Port, string Target);
