using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.Dns.HttpsRecord;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1658.c</c>: <c>doh_resp_decode_httpsrr</c>, here
/// <see cref="ServiceBindingRecordDecoder.Decode"/>, over the C program's 30 rows in order, from
/// "single h2 alpn" to "rname long label". Each row's record data is decoded and rendered as the C
/// program's <c>rrresults</c> renders it - the <see cref="CurlCode"/>, then for a decoded record its
/// priority, target, ALPN ids, <c>no-def-alpn</c>, port, IPv4 hints, ECH bytes and IPv6 hints - and the
/// text compared with the row's. It writes <c>test N: name</c> for each row to standard output and the
/// C program's three lines to standard error for each row that differs.
/// </summary>
/// <param name="decode">Decodes one record's data; <see cref="ServiceBindingRecordDecoder.Decode"/> unless a test injects another.</param>
public sealed class Unit1658HttpsRecordDecoding(Func<byte[], ServiceBindingDecoding> decode) : IUnitProgram
{
    private static readonly (string Name, byte[] Data, string Expected)[] Rows =
    [
        ("single h2 alpn", Hex("0000" + "046E616D6500" + "0001" + "0003" + "026832"), "r:0|p:0|name.|alpn:10|"),
        ("single h2 alpn missing last byte", Hex("0000" + "046E616D6500" + "0001" + "0003" + "0268"), "r:8|"),
        ("two alpns", Hex("0000" + NameSome + "0001" + "0006" + "026832" + "026833"), "r:0|p:0|name.some.|alpn:10|alpn:20|"),
        ("wrong syntax alpns", Hex("0000" + NameSome + "0001" + "0006" + "026832" + "036833"), "r:61|"),
        ("five alpns (ignore dupes)", Hex("0000" + NameSome + "0001" + "000F" + "026832026832026832026832" + "026833"), "r:0|p:0|name.some.|alpn:10|alpn:20|"),
        ("rname only", Hex("0000" + NameSome), "r:0|p:0|name.some.|"),
        ("rname with low ascii byte", Hex("0000" + "046E616D6504736F6D0300"), "r:8|"),
        ("rname with null byte", Hex("0000" + "0473610E04736F6D650000"), "r:27|"),
        ("rname only (missing byte)", Hex("0000" + "046E616D6505736F6D6500"), "r:27|"),
        ("unrecognized alpn", Hex("0000" + NameSome + "0001" + "0006" + "026838" + "026831"), "r:0|p:0|name.some.|alpn:8|"),
        ("alnt + no-default-alpn", Hex("0000" + NameSome + "0001" + "0003" + "026832" + "0002" + "0000"), "r:0|p:0|name.some.|alpn:10|no-def-alpn|"),
        ("alnt + no-default-alpn with size", Hex("0000" + NameSome + "0001" + "0003" + "026832" + "0002" + "0001" + "FF"), "r:43|"),
        ("alnt + no-default-alpn with size too short package", Hex("0000" + NameSome + "0001" + "0003" + "026832" + "0002" + "0001"), "r:8|"),
        ("rname + blank alpn field", Hex("1111" + NameSome + "0001" + "0000"), "r:0|p:4369|name.some.|"),
        ("no rname + blank alpn", Hex("0011" + "00" + "0001" + "0000"), "r:0|p:17|.|"),
        ("unsupported field", Hex("FFFF" + "00" + "0007" + "0002" + "4646"), "r:0|p:65535|.|"),
        ("unsupported field (wrong size)", Hex("FFFF" + "00" + "0007" + "0002" + "46"), "r:8|"),
        ("port number", Hex("0010" + "00" + AlpnH2 + "0003" + "0002" + "1234"), "r:0|p:16|.|alpn:10|port:4660|"),
        ("port number with wrong size (3 bytes)", Hex("0010" + "00" + AlpnH2 + "0003" + "0003" + "123400"), "r:43|"),
        ("port number with wrong size (1 byte)", Hex("0010" + "00" + AlpnH2 + "0003" + "0001" + "12"), "r:43|"),
        ("alpn + two IPv4 addresses", Hex("0010" + "00" + AlpnH2 + "0004" + "0008" + "C0A80001" + "C0A80002"), "r:0|p:16|.|alpn:10|ipv4:192.168.0.1|ipv4:192.168.0.2|"),
        ("alpn + two IPv4 addresses in wrong order", Hex("0010" + "00" + "0004" + "0008" + "C0A80001" + "C0A80002" + AlpnH2), "r:8|"),
        ("alpn + IPv4 address with wrong size", Hex("0010" + "00" + AlpnH2 + "0004" + "0005" + "C0A80001FF"), "r:43|"),
        ("alpn + one IPv6 address", Hex("0010" + "00" + AlpnH2 + "0006" + "0010" + Ipv6A), "r:0|p:16|.|alpn:10|ipv6:fe80:dabb:c1ff:fea3:8a22:1234:5678:9123|"),
        ("alpn + one IPv6 address with wrong size", Hex("0010" + "00" + AlpnH2 + "0006" + "0011" + Ipv6A + "45"), "r:43|"),
        ("alpn + two IPv6 addresses", Hex("0010" + "00" + AlpnH2 + "0006" + "0020" + Ipv6A + Ipv6B),
            "r:0|p:16|.|alpn:10|ipv6:fe80:dabb:c1ff:fea3:8a22:1234:5678:9123|ipv6:ee80:dabb:c1ff:fea3:8a22:1234:5678:9125|"),
        ("alpn + ech", Hex("0010" + "00" + AlpnH2 + "0005" + "0010" + Ipv6A), "r:0|p:16|.|alpn:10|ech:fe80dabbc1fffea38a22123456789123|"),
        ("fully packed",
            Hex("A00B" + "00" + "0000" + "0000" + "0001" + "0006" + "026832" + "026831" + "0002" + "0000" + "0003" + "0002" + "BC71"
                + "0004" + "0008" + "C0A80001" + "C0A80002" + "0005" + "0010" + "FE80DABBC1FF7EB38A22123456789123"
                + "0006" + "0020" + Ipv6A + Ipv6B + "0107" + "0004" + "46464141"),
            "r:0|p:40971|.|alpn:10|alpn:8|no-def-alpn|port:48241|ipv4:192.168.0.1|ipv4:192.168.0.2|"
            + "ech:fe80dabbc1ff7eb38a22123456789123|ipv6:fe80:dabb:c1ff:fea3:8a22:1234:5678:9123|ipv6:ee80:dabb:c1ff:fea3:8a22:1234:5678:9125|"),
        ("rname too long label", Hex("0000" + "40" + Ascii("0123456789012345678901234567890123456789012345678901234567890123") + "04736F6D6500"), "r:27|"),
        ("rname long label", Hex("0000" + "3F" + Ascii("012345678901234567890123456789012345678901234567890123456789012") + "04736F6D6500"),
            "r:0|p:0|012345678901234567890123456789012345678901234567890123456789012.some.|"),
    ];

    /// <summary>Creates the port over <see cref="ServiceBindingRecordDecoder.Decode"/>.</summary>
    public Unit1658HttpsRecordDecoding()
        : this(data => ServiceBindingRecordDecoder.Decode(data))
    {
    }

    /// <summary><c>\x04name\x04some\x00</c>.</summary>
    private static string NameSome => "046E616D6504736F6D6500";

    /// <summary>An <c>alpn</c> parameter holding <c>h2</c>.</summary>
    private static string AlpnH2 => "0001" + "0003" + "026832";

    private static string Ipv6A => "FE80DABBC1FFFEA38A22123456789123";

    private static string Ipv6B => "EE80DABBC1FFFEA38A22123456789125";

    /// <inheritdoc />
    public string ProgramName => "unit1658";

    /// <summary>
    /// Renders a decoding as the C program's <c>rrresults</c> does: <c>r:&lt;CURLcode&gt;|</c>, then for a
    /// decoded record <c>p:</c>, the target with its trailing dot, each ALPN id in hex (curl keeps
    /// <c>h1</c>, <c>h2</c>, <c>h3</c> and <c>http/1.1</c> only, without repeats, so its limit of four never binds),
    /// <c>no-def-alpn</c>, <c>port:</c>, each <c>ipv4:</c>, <c>ech:</c> in hex and each <c>ipv6:</c>, all ending in <c>|</c>.
    /// </summary>
    /// <param name="decoding">The decoder's outcome.</param>
    /// <returns>The rendering.</returns>
    public static string Render(ServiceBindingDecoding decoding)
    {
        ArgumentNullException.ThrowIfNull(decoding);
        StringBuilder text = new($"r:{(int)CodeOf(decoding.Failure)}|");
        if (decoding.Record is { } record)
        {
            text.Append(CultureInfo.InvariantCulture, $"p:{record.Priority}|{TargetOf(record)}|");
            AppendAlpnIds(text, record.ApplicationProtocols);
            AppendParameters(text, record);
        }

        return text.ToString();
    }

    /// <inheritdoc />
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int failures = 0;
        await using StreamWriter output = new(context.StandardOutput, new UTF8Encoding(false), leaveOpen: true);
        for (int index = 0; index < Rows.Length; index++)
        {
            (string name, byte[] data, string expected) = Rows[index];
            await output.WriteAsync($"test {index}: {name}\n".AsMemory(), cancellationToken).ConfigureAwait(false);
            string received = Render(decode(data));
            if (received != expected)
            {
                await context.StandardError.WriteAsync($"Test {name} ({index}) failed\nExpected: {expected}\nReceived: {received}\n").ConfigureAwait(false);
                failures++;
            }
        }

        return new UnitProgramResult(failures);
    }

    /// <summary>The <c>CURLcode</c> curl's <c>doh_resp_decode_httpsrr</c> returns for each failure.</summary>
    private static CurlCode CodeOf(ServiceBindingFailure failure) => failure switch
    {
        ServiceBindingFailure.None => CurlCode.Ok,
        ServiceBindingFailure.BadTargetName => CurlCode.OutOfMemory,
        ServiceBindingFailure.TargetNameHasControlCharacter or ServiceBindingFailure.ParametersOutOfOrder or ServiceBindingFailure.ParameterOverrun => CurlCode.WeirdServerReply,
        ServiceBindingFailure.BadApplicationProtocolList => CurlCode.BadContentEncoding,
        _ => CurlCode.BadFunctionArgument,
    };

    /// <summary>The target as curl's C string holds it: dotted with a trailing dot, <c>.</c> for the root, ending at any NUL.</summary>
    private static string TargetOf(ServiceBindingRecord record)
    {
        string target = record.TargetName == "." ? "." : record.TargetName + ".";
        int end = target.IndexOf('\0', StringComparison.Ordinal);
        return end < 0 ? target : target[..end];
    }

    private static void AppendAlpnIds(StringBuilder text, IReadOnlyList<string> applicationProtocols)
    {
        List<int> ids = [];
        foreach (string protocol in applicationProtocols)
        {
            int id = AlpnIdOf(protocol);
            if (id != 0 && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        foreach (int id in ids)
        {
            text.Append(CultureInfo.InvariantCulture, $"alpn:{id:x}|");
        }
    }

    /// <summary>curl's <c>Curl_alpn2alpnid</c>: <c>ALPN_h1</c> 8, <c>ALPN_h2</c> 16, <c>ALPN_h3</c> 32, else <c>ALPN_none</c> 0.</summary>
    private static int AlpnIdOf(string protocol) => protocol switch
    {
        "h1" or "http/1.1" => 8,
        "h2" => 16,
        "h3" => 32,
        _ => 0,
    };

    private static void AppendParameters(StringBuilder text, ServiceBindingRecord record)
    {
        if (record.NoDefaultApplicationProtocol)
        {
            text.Append("no-def-alpn|");
        }

        if (record.Port is { } port)
        {
            text.Append(CultureInfo.InvariantCulture, $"port:{port}|");
        }

        AppendHintsAndEch(text, record);
    }

    private static void AppendHintsAndEch(StringBuilder text, ServiceBindingRecord record)
    {
        foreach (var address in record.IPv4Hints)
        {
            text.Append(CultureInfo.InvariantCulture, $"ipv4:{address}|");
        }

        if (!record.EchConfigList.IsEmpty)
        {
            text.Append(CultureInfo.InvariantCulture, $"ech:{Convert.ToHexStringLower(record.EchConfigList.Span)}|");
        }

        foreach (var address in record.IPv6Hints)
        {
            text.Append("ipv6:").AppendJoin(':', address.GetAddressBytes().Chunk(2).Select(Convert.ToHexStringLower)).Append('|');
        }
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static string Ascii(string text) => Convert.ToHexString(Encoding.ASCII.GetBytes(text));
}
