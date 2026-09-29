namespace Curl.Networking;

/// <summary>One SRV record of a DNS answer (RFC 2782).</summary>
/// <param name="Priority">The priority: a client tries the lowest first.</param>
/// <param name="Weight">The relative weight among records of the same priority.</param>
/// <param name="Port">The port the service listens on.</param>
/// <param name="Target">The host the service runs on, dotted, with no trailing dot; empty for the root, "service not available".</param>
public sealed record DnsServiceRecord(ushort Priority, ushort Weight, ushort Port, string Target);
