namespace Curl.Tls;

/// <summary>One identity a ClientHello offers in <c>pre_shared_key</c> (RFC 8446 section 4.2.11).</summary>
/// <param name="Identity">The identity: a ticket, or an external PSK's label.</param>
/// <param name="ObfuscatedTicketAge">The ticket age in milliseconds plus the ticket's age add, modulo 2^32.</param>
public sealed record PskIdentity(byte[] Identity, uint ObfuscatedTicketAge);
