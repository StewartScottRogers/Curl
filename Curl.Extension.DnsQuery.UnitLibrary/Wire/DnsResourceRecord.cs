namespace Curl.Extension.DnsQuery.Wire;

/// <summary>One resource record of an answer, authority or additional section (RFC 1035 section 4.1.3).</summary>
/// <param name="Name">The owner name in presentation format, ending in a dot.</param>
/// <param name="Type">The TYPE.</param>
/// <param name="Class">The CLASS.</param>
/// <param name="TimeToLive">The TTL in seconds.</param>
/// <param name="Data">The RDATA's fields, in wire order.</param>
internal sealed record DnsResourceRecord(string Name, ushort Type, ushort Class, uint TimeToLive, IReadOnlyList<DnsRecordField> Data);
