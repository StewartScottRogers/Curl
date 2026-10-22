using System.Net;
using System.Text;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.Dns.Doh;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1650.c</c>: <c>doh_req_encode</c>, here
/// <see cref="DnsQueryEncoder.Encode"/>, and <c>doh_resp_decode</c>, here <see cref="DnsAnswerDecoder.Decode"/>,
/// over the C program's checks in order - the three request rows (<c>test.host.name</c> as A and as AAAA
/// byte for byte, and a 64-byte label refused with <c>DOH_DNS_BAD_LABEL</c>); the fourteen response rows,
/// each decode's return code and, where the row names one, its addresses and TTL as the C program prints
/// them (<c>127.0.0.1 (55)</c>; an AAAA address as eight four-digit groups); every prefix of the 49-byte
/// <c>foo.example.com</c> answer, and every piece from its other end, failing; and the whole answer
/// decoding to its one address <c>127.0.0.1</c>. A <c>DOHcode</c> is the <see cref="DnsMessageFailure"/>
/// of the same name (<c>DOH_TOO_SMALL_BUFFER</c> is <see cref="DnsMessageFailure.TooSmall"/>). It writes
/// <c>test N: name</c> for each check to standard output and three lines to standard error for each check
/// that differs.
/// </summary>
/// <param name="encode">Encodes one query; <see cref="DnsQueryEncoder.Encode"/> unless a test injects another.</param>
/// <param name="decode">Decodes one answer; <see cref="DnsAnswerDecoder.Decode"/> unless a test injects another.</param>
public sealed class Unit1650DohEncodingAndDecoding(
    Func<string, DnsRecordType, DnsQueryEncoding> encode,
    Func<byte[], DnsRecordType, DnsAnswer> decode) : IUnitProgram
{
    private const string Preamble = "000001000001000000000000";
    private const string TestHostName = "0474657374" + "04686f7374" + "046e616d65";
    private const string AaaaRecordTail = "00001c0001" + "00000001" + "0004" + "01010101";
    private const string AaaaAnswer = "0461616161" + "076578616d706c65" + "03636f6d00" + "001c0001" + "c00c001c0001";
    private const string AaaaData = "0010" + "20202020" + "00000000000000000000" + "2020";
    private const string CurlCname = "046375726c" + "046375726c00" + "00050001" + "c00c00050001" + "00000037";
    private const string FailedDecode = "Failed";

    /// <summary>The C program's <c>full49</c>: <c>foo.example.com</c> A 127.0.0.1, TTL 55.</summary>
    private static readonly byte[] Full49 = Convert.FromHexString(
        "000001000001000100000000" + "03666f6f" + "076578616d706c65" + "03636f6d00" + "00010001" + "c00c00010001" + "00000037" + "0004" + "7f000001");

    private static readonly (string Name, string HostName, DnsRecordType Type, string Expected)[] RequestRows =
    [
        ("req 0: test.host.name A", "test.host.name", DnsRecordType.A, "None|" + Preamble + TestHostName + "0000010001"),
        ("req 1: test.host.name AAAA", "test.host.name", DnsRecordType.Aaaa, "None|" + Preamble + TestHostName + "00001c0001"),
        ("req 2: 64-byte label", new string('z', 64) + ".host.name", DnsRecordType.Aaaa, "BadLabel|"),
    ];

    private static readonly (string Name, string Packet, DnsRecordType Type, string Expected)[] ResponseRows =
    [
        ("resp 0: two bytes", "0000", DnsRecordType.A, "TooSmall"),
        ("resp 1: ID 1", "000100010001000100010001", DnsRecordType.A, "BadId"),
        ("resp 2: RCODE 1", "000000010001000100010001", DnsRecordType.A, "BadRcode"),
        ("resp 3: RCODE 3", "000000030001000100010001", DnsRecordType.A, "NxDomain"),
        ("resp 4: question cut in its name", "00000100000100010000000003666f6f", DnsRecordType.A, "OutOfRange"),
        ("resp 5: question without its type", "00000100000100010000000003666f6f00", DnsRecordType.A, "OutOfRange"),
        ("resp 6: no answer record", "00000100000100010000000003666f6f0000010001", DnsRecordType.A, "OutOfRange"),
        ("resp 7: question cut in its type", "00000100000100010000000003666f6f0000", DnsRecordType.A, "OutOfRange"),
        ("resp 8: CNAME only", "000001000001000100000000" + CurlCname + "0011" + "08616e797768657265" + "067265616c6c7900", DnsRecordType.A, "None"),
        ("resp 9: foo.example.com A", Convert.ToHexString(Full49), DnsRecordType.A, "None|127.0.0.1 (55)"),
        ("resp 10: aaaa.example.com AAAA", "000001000001000100000000" + AaaaAnswer + "00000137" + AaaaData, DnsRecordType.Aaaa, "None|2020:2020:0000:0000:0000:0000:0000:2020 (311)"),
        ("resp 11: self-pointing CNAME", "000001000001000100000000" + CurlCname + "0007" + "03616e79c02700", DnsRecordType.A, "None"),
        ("resp 12: NSCOUNT 1", "000001000001000100010000" + AaaaAnswer + "00000037" + AaaaData + TestHostName + AaaaRecordTail, DnsRecordType.Aaaa, "None|2020:2020:0000:0000:0000:0000:0000:2020 (55)"),
        ("resp 13: ARCOUNT 1, TTL capped", "000001000001000100000001" + AaaaAnswer + "00ffff37" + AaaaData + TestHostName + AaaaRecordTail, DnsRecordType.Aaaa, "None|2020:2020:0000:0000:0000:0000:0000:2020 (86400)"),
    ];

    /// <summary>Creates the port over <see cref="DnsQueryEncoder.Encode"/> and <see cref="DnsAnswerDecoder.Decode"/>.</summary>
    public Unit1650DohEncodingAndDecoding()
        : this(DnsQueryEncoder.Encode, (message, type) => DnsAnswerDecoder.Decode(message, type))
    {
    }

    /// <inheritdoc/>
    public string ProgramName => "unit1650";

    /// <summary>Renders an encoding as the C program checks it: the return code, then the packet in hex.</summary>
    /// <param name="encoding">What the encoder returned.</param>
    /// <returns>The failure and the bytes in lower-case hex, separated by <c>|</c>.</returns>
    public static string RenderRequest(DnsQueryEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        return $"{encoding.Failure}|{Convert.ToHexStringLower(encoding.Bytes)}";
    }

    /// <summary>
    /// Renders a decode as the C program prints it: the return code and, when <paramref name="withAddresses"/>,
    /// <c>|</c> and each address followed by a space (A as dotted quads, AAAA as eight <c>%02x%02x</c> groups)
    /// and the TTL in brackets.
    /// </summary>
    /// <param name="answer">What the decoder returned.</param>
    /// <param name="withAddresses">Whether the row names the addresses and TTL.</param>
    /// <returns>The rendered decode.</returns>
    public static string RenderResponse(DnsAnswer answer, bool withAddresses)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (!withAddresses)
        {
            return answer.Failure.ToString();
        }

        StringBuilder text = new($"{answer.Failure}|");
        foreach (IPAddress address in answer.Addresses)
        {
            text.Append(RenderAddress(address)).Append(' ');
        }

        return text.Append($"({answer.TimeToLiveSeconds})").ToString();
    }

    /// <inheritdoc/>
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int failures = 0;
        int index = 0;
        await using StreamWriter output = new(context.StandardOutput, new UTF8Encoding(false), leaveOpen: true);
        foreach ((string name, string expected, Func<string> receive) in Checks())
        {
            await output.WriteAsync($"test {index}: {name}\n".AsMemory(), cancellationToken).ConfigureAwait(false);
            string received = receive();
            if (received != expected)
            {
                await context.StandardError.WriteAsync($"Test {name} ({index}) failed\nExpected: {expected}\nReceived: {received}\n").ConfigureAwait(false);
                failures++;
            }

            index++;
        }

        return new UnitProgramResult(failures);
    }

    private static string RenderAddress(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes.Length == 4
            ? address.ToString()
            : string.Join(':', Enumerable.Range(0, 8).Select(group => Convert.ToHexStringLower(bytes, group * 2, 2)));
    }

    private IEnumerable<(string Name, string Expected, Func<string> Receive)> Checks()
    {
        foreach ((string name, string hostName, DnsRecordType type, string expected) in RequestRows)
        {
            yield return (name, expected, () => RenderRequest(encode(hostName, type)));
        }

        foreach ((string name, string packet, DnsRecordType type, string expected) in ResponseRows)
        {
            yield return (name, expected, () => RenderResponse(decode(Convert.FromHexString(packet), type), expected.Contains('|')));
        }

        for (int length = 0; length < Full49.Length; length++)
        {
            byte[] prefix = Full49[..length];
            yield return ($"full49 cut to {length} bytes", FailedDecode, () => DecodeFails(prefix));
        }

        for (int start = 1; start <= Full49.Length; start++)
        {
            byte[] suffix = Full49[start..];
            yield return ($"full49 from byte {start}", FailedDecode, () => DecodeFails(suffix));
        }

        yield return ("full49 whole", "None|127.0.0.1 (55)", () => RenderResponse(decode(Full49, DnsRecordType.A), withAddresses: true));
    }

    private string DecodeFails(byte[] message) =>
        decode(message, DnsRecordType.A).Failure == DnsMessageFailure.None ? nameof(DnsMessageFailure.None) : FailedDecode;
}
