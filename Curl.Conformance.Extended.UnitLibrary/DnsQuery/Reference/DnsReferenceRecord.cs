using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>
/// One resource record a <see cref="DnsReferenceServer" /> answers with (RFC 1035 section 4.1.3), its RDATA already in
/// wire form. The factories write the RDATA of each type from its specification: RFC 1035 section 3.3 and 3.4 (A, NS,
/// CNAME, PTR, MX, TXT, SOA), RFC 3596 (AAAA), RFC 2782 (SRV) and RFC 8659 (CAA); any other type is given as raw RDATA
/// (RFC 3597). Names in RDATA are written in full, never compressed.
/// </summary>
/// <param name="Name">The owner name, such as <c>example.com.</c>.</param>
/// <param name="Type">The TYPE code.</param>
/// <param name="Class">The CLASS code.</param>
/// <param name="TimeToLive">The TTL in seconds.</param>
/// <param name="Data">The RDATA.</param>
public sealed record DnsReferenceRecord(string Name, ushort Type, ushort Class, uint TimeToLive, byte[] Data)
{
    /// <summary>The IN class (RFC 1035 section 3.2.4).</summary>
    public const ushort InternetClass = 1;

    /// <summary>An A record (RFC 1035 section 3.4.1).</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="address">The IPv4 address.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord A(string name, uint timeToLive, string address) =>
        new(name, 1, InternetClass, timeToLive, IPAddress.Parse(address).GetAddressBytes());

    /// <summary>An AAAA record (RFC 3596 section 2.2).</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="address">The IPv6 address.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord Aaaa(string name, uint timeToLive, string address) =>
        new(name, 28, InternetClass, timeToLive, IPAddress.Parse(address).GetAddressBytes());

    /// <summary>A record whose RDATA is one domain name: NS (2), CNAME (5) or PTR (12) (RFC 1035 sections 3.3.11, 3.3.1 and 3.3.12).</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="type">The TYPE code.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="target">The domain name the RDATA holds.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord OfName(string name, ushort type, uint timeToLive, string target) =>
        new(name, type, InternetClass, timeToLive, DnsReferenceName.Encode(target));

    /// <summary>An MX record (RFC 1035 section 3.3.9).</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="preference">The PREFERENCE.</param>
    /// <param name="exchange">The EXCHANGE host.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord Mx(string name, uint timeToLive, ushort preference, string exchange) =>
        new(name, 15, InternetClass, timeToLive, [.. UInt16(preference), .. DnsReferenceName.Encode(exchange)]);

    /// <summary>A TXT record (RFC 1035 section 3.3.14): each string as a length octet and its octets.</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="strings">The character-strings, each at most 255 octets of ASCII.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord Txt(string name, uint timeToLive, params string[] strings) =>
        new(name, 16, InternetClass, timeToLive, [.. strings.SelectMany(CharacterString)]);

    /// <summary>An SOA record (RFC 1035 section 3.3.13).</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="primary">The MNAME.</param>
    /// <param name="mailbox">The RNAME.</param>
    /// <param name="timers">SERIAL, REFRESH, RETRY, EXPIRE and MINIMUM, in that order.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord Soa(string name, uint timeToLive, string primary, string mailbox, params uint[] timers) =>
        new(name, 6, InternetClass, timeToLive, [.. DnsReferenceName.Encode(primary), .. DnsReferenceName.Encode(mailbox), .. timers.SelectMany(UInt32)]);

    /// <summary>An SRV record (RFC 2782).</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="priority">The Priority.</param>
    /// <param name="weight">The Weight.</param>
    /// <param name="port">The Port.</param>
    /// <param name="target">The Target host.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord Srv(string name, uint timeToLive, ushort priority, ushort weight, ushort port, string target) =>
        new(name, 33, InternetClass, timeToLive, [.. UInt16(priority), .. UInt16(weight), .. UInt16(port), .. DnsReferenceName.Encode(target)]);

    /// <summary>A CAA record (RFC 8659 section 4.1): the flags octet, the tag as a length octet and its octets, then the value to the end.</summary>
    /// <param name="name">The owner name.</param>
    /// <param name="timeToLive">The TTL.</param>
    /// <param name="flags">The Flags octet.</param>
    /// <param name="tag">The property tag, such as <c>issue</c>.</param>
    /// <param name="value">The property value.</param>
    /// <returns>The record.</returns>
    public static DnsReferenceRecord Caa(string name, uint timeToLive, byte flags, string tag, string value) =>
        new(name, 257, InternetClass, timeToLive, [flags, .. CharacterString(tag), .. Encoding.ASCII.GetBytes(value)]);

    private static byte[] CharacterString(string text) => [(byte)text.Length, .. Encoding.ASCII.GetBytes(text)];

    private static byte[] UInt16(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
