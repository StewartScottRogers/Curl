using Curl.Extended.Protocol.DnsQuery.UnitTests.Wire;
using Curl.Extended.Protocol.DnsQuery.Wire;

namespace Curl.Extended.Protocol.DnsQuery.UnitTests.Rendering;

/// <summary>One RDATA per record type the renderers present, decoded as the single answer of a response.</summary>
internal static class DnsSampleRecords
{
    public static DnsMessage Decode(string sample) => DnsMessageDecoder.Decode(DnsWire.SingleAnswer(Type(sample), Data(sample))).Value!;

    private static int Type(string sample) => sample switch
    {
        "A" => 1,
        "AAAA" => 28,
        "NS" => 2,
        "CNAME" => 5,
        "PTR" => 12,
        "MX" => 15,
        "TXT" => 16,
        "SOA" => 6,
        "SRV" => 33,
        "CAA" => 257,
        "TYPE65" => 65,
        _ => 99,
    };

    private static byte[] Data(string sample) => sample switch
    {
        "A" => [93, 184, 216, 34],
        "AAAA" => [0x20, 0x01, 0x0D, 0xB8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1],
        "NS" => DnsWire.Join([3, (byte)'n', (byte)'s', (byte)'1'], DnsWire.Pointer(DnsWire.FirstQuestionName)),
        "CNAME" => DnsWire.Name("www.example.net"),
        "PTR" => DnsWire.Join([4, (byte)'a', (byte)'.', (byte)'b', (byte)' '], DnsWire.Name("example")),
        "MX" => DnsWire.Join(DnsWire.UInt16(10), DnsWire.Name("mail.example.com")),
        "TXT" => DnsWire.Join(DnsWire.CharacterString("v=spf1 -all"), DnsWire.CharacterString("a\"b\\"), [1, 0xFF]),
        "SOA" => DnsWire.Join(
            DnsWire.Name("ns1.example.com"),
            DnsWire.Name("hostmaster.example.com"),
            DnsWire.UInt32(2024010101),
            DnsWire.UInt32(7200),
            DnsWire.UInt32(3600),
            DnsWire.UInt32(1209600),
            DnsWire.UInt32(300)),
        "SRV" => DnsWire.Join(DnsWire.UInt16(10), DnsWire.UInt16(20), DnsWire.UInt16(443), DnsWire.Name("sip.example.com")),
        "CAA" => DnsWire.Join([128], DnsWire.CharacterString("issue"), "letsencrypt.org"u8.ToArray()),
        "TYPE65" => [0x01, 0xAB],
        _ => [],
    };
}
