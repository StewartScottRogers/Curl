using System.Text;
using Curl.Networking;

namespace Curl.Conformance.UnitProgram.Dns.Doh;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1655.c</c>: <c>doh_req_encode</c>, here
/// <see cref="DnsQueryEncoder.Encode"/>, over the C program's checks in order - the name too long
/// (<c>DOH_DNS_NAME_TOO_LONG</c>), the empty label and the 64-byte label (<c>DOH_DNS_BAD_LABEL</c>),
/// <c>a.com</c> and <c>a.com.</c> encoding to the same length and <c>aa.com</c> to one more, and the
/// maximum-length name filling the 272-byte buffer. The C program's canary and overflow checks hold by
/// construction in C#, since the encoder allocates exactly the query's length and never writes past
/// a span: they are ported as "the maximum-length name is refused by a buffer one byte too small"
/// (<c>DOH_TOO_SMALL_BUFFER</c>) and "every refused name writes nothing". It writes <c>test N: name</c>
/// for each check to standard output and three lines to standard error for each check that differs.
/// </summary>
/// <param name="encode">Encodes one query; <see cref="DnsQueryEncoder.Encode"/> unless a test injects another.</param>
public sealed class Unit1655DohRequestEncoding(Func<string, DnsRecordType, DnsQueryEncoding> encode) : IUnitProgram
{
    /// <summary>The length of the C program's query buffer, curl's largest DoH query.</summary>
    private const int BufferLength = 256 + 16;

    /// <summary>A 255-byte name with a trailing dot, which encodes to exactly <see cref="BufferLength"/> bytes.</summary>
    private static readonly string MaximumName = $"{Label(63)}.{Label(63)}.{Label(63)}.{Label(62)}.";

    /// <summary>A 256-byte name with a trailing dot, one byte past what curl encodes.</summary>
    private static readonly string TooLongName = $"{Label(63)}.{Label(63)}.{Label(63)}.{Label(63)}.";

    private static readonly (string Name, string HostName, int BufferLength, string Expected)[] Rows =
    [
        ("name too long", TooLongName, BufferLength, "NameTooLong|0"),
        ("empty label", "a..b", BufferLength, "BadLabel|0"),
        ("64-byte label", Label(64) + ".com", BufferLength, "BadLabel|0"),
        ("a.com", "a.com", BufferLength, "None|23"),
        ("a.com. (trailing dot, same length)", "a.com.", BufferLength, "None|23"),
        ("aa.com (one longer)", "aa.com", BufferLength, "None|24"),
        ("maximum-length name", MaximumName, BufferLength, "None|272"),
        ("maximum-length name, buffer one byte too small", MaximumName, BufferLength - 1, "TooSmall|0"),
    ];

    /// <summary>Creates the port over <see cref="DnsQueryEncoder.Encode"/>.</summary>
    public Unit1655DohRequestEncoding()
        : this(DnsQueryEncoder.Encode)
    {
    }

    /// <inheritdoc/>
    public string ProgramName => "unit1655";

    /// <summary>
    /// Emulates <c>doh_req_encode(name, A, buffer, bufferLength, &amp;olen)</c>: the encoder's failure if
    /// any, else <see cref="DnsMessageFailure.TooSmall"/> when the query does not fit the buffer, else
    /// the query's length; rendered as <c>failure|olen</c>, with <c>olen</c> 0 whenever nothing is written.
    /// </summary>
    /// <param name="encoding">What the encoder returned.</param>
    /// <param name="bufferLength">The length of the buffer the C program passes.</param>
    /// <returns>The failure and the bytes written, separated by <c>|</c>.</returns>
    public static string Render(DnsQueryEncoding encoding, int bufferLength)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        if (encoding.Failure != DnsMessageFailure.None)
        {
            return $"{encoding.Failure}|{encoding.Bytes.Length}";
        }

        return encoding.Bytes.Length > bufferLength ? $"{DnsMessageFailure.TooSmall}|0" : $"None|{encoding.Bytes.Length}";
    }

    /// <inheritdoc/>
    public async Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int failures = 0;
        await using StreamWriter output = new(context.StandardOutput, new UTF8Encoding(false), leaveOpen: true);
        for (int index = 0; index < Rows.Length; index++)
        {
            (string name, string hostName, int bufferLength, string expected) = Rows[index];
            await output.WriteAsync($"test {index}: {name}\n".AsMemory(), cancellationToken).ConfigureAwait(false);
            string received = Render(encode(hostName, DnsRecordType.A), bufferLength);
            if (received != expected)
            {
                await context.StandardError.WriteAsync($"Test {name} ({index}) failed\nExpected: {expected}\nReceived: {received}\n").ConfigureAwait(false);
                failures++;
            }
        }

        return new UnitProgramResult(failures);
    }

    private static string Label(int length) => new('a', length);
}
