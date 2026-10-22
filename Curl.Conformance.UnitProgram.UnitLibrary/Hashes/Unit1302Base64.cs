using System.Buffers.Text;
using System.Text;
using Curl.Cli;

namespace Curl.Conformance.UnitProgram.Hashes;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1302.c</c>: <c>curlx_base64_encode</c> (the BCL's
/// <see cref="Convert.ToBase64String(byte[])"/>, which Curl encodes with), <c>curlx_base64url_encode</c>
/// (the BCL's <see cref="Base64Url"/>) and <c>curlx_base64_decode</c> (Curl's <see cref="CurlBase64Decoder"/>),
/// over the C program's three tables row for row.
/// </summary>
public sealed class Unit1302Base64 : IUnitProgram
{
    /// <summary>The common encoding table: input, how many of its bytes are encoded, and the encoding.</summary>
    private static readonly (string Input, int Length, string Output)[] EncodeRows =
    [
        ("iiiiii", 1, "aQ=="), ("iiiiii", 2, "aWk="), ("iiiiii", 3, "aWlp"), ("iiiiii", 4, "aWlpaQ=="),
        ("iiiiii", 5, "aWlpaWk="), ("iiiiii", 6, "aWlpaWlp"), ("iiiiiii", 7, "aWlpaWlpaQ=="),
        ("iiiiiiii", 8, "aWlpaWlpaWk="), ("iiiiiiiii", 9, "aWlpaWlpaWlp"), ("iiiiiiiiii", 10, "aWlpaWlpaWlpaQ=="),
        ("iiiiiiiiiii", 11, "aWlpaWlpaWlpaWk="), ("iiiiiiiiiiii", 12, "aWlpaWlpaWlpaWlp"),
        ("ÿ\u0001þ\u0002", 4, "/wH+Ag=="), ("ÿÿÿÿ", 4, "/////w=="),
        ("\0\0\0\0", 4, "AAAAAA=="), ("\0\0\0\0", 1, "AA=="),
    ];

    /// <summary>The URL encoding table, in the same shape.</summary>
    private static readonly (string Input, int Length, string Output)[] UrlRows =
    [
        (string.Empty, 0, string.Empty), ("iiiiiiiiiii", 1, "aQ"), ("iiiiiiiiiii", 2, "aWk"), ("iiiiiiiiiii", 3, "aWlp"),
        ("iiiiiiiiiii", 4, "aWlpaQ"), ("iiiiiiiiiii", 5, "aWlpaWk"), ("iiiiiiiiiii", 6, "aWlpaWlp"),
        ("iiiiiiiiiii", 7, "aWlpaWlpaQ"), ("iiiiiiiiiii", 8, "aWlpaWlpaWk"), ("iiiiiiiiiii", 9, "aWlpaWlpaWlp"),
        ("iiiiiiiiiii", 10, "aWlpaWlpaWlpaQ"), ("iiiiiiiiiii", 11, "aWlpaWlpaWlpaWk"), ("iiiiiiiiiiii", 12, "aWlpaWlpaWlpaWlp"),
        ("ÿ\u0001þ\u0002", 4, "_wH-Ag"), ("ÿÿÿÿ", 4, "_____w"),
        ("ÿ\0ÿ\0", 4, "_wD_AA"), ("\0ÿ\0ÿ", 4, "AP8A_w"), ("\0\0\0\0", 4, "AAAAAA"),
        ("\0", 1, "AA"), ("\u0001", 1, "AQ"), ("\u0002", 1, "Ag"), ("\u0003", 1, "Aw"), ("\u0004", 1, "BA"),
        ("\u0005", 1, "BQ"), ("\u0006", 1, "Bg"), ("\u0007", 1, "Bw"), ("\u0008", 1, "CA"), ("\u0009", 1, "CQ"),
        ("\u000a", 1, "Cg"), ("\u000b", 1, "Cw"), ("\u000c", 1, "DA"), ("\u000d", 1, "DQ"), ("\u000e", 1, "Dg"),
        ("\u000f", 1, "Dw"), ("\u0010", 1, "EA"),
    ];

    /// <summary>The bad decode table: text <c>curlx_base64_decode</c> refuses with <c>CURLE_BAD_CONTENT_ENCODING</c>.</summary>
    private static readonly string[] BadDecodeRows =
    [
        string.Empty, "a", "aQ", "aQ=", "====", "====", "====", "====", "a===", "a=Q=", "aWlpa=Q=", "a\u001f==",
        "abcd ", "abcd  ", " abcd", "_abcd", "abcd-", "abcd_", "aWlpaWlpaQ==-", "aWlpaWlpaQ==_", "aWlpaWlpaQ== ",
        "aWlpaWlpaQ=",
    ];

    /// <inheritdoc />
    public string ProgramName => "unit1302";

    /// <inheritdoc />
    public Task<UnitProgramResult> RunAsync(UnitProgramContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        UnitChecks checks = new(context.StandardError, ProgramName);
        for (int row = 0; row < EncodeRows.Length; row++)
        {
            CheckEncodeAndDecode(checks, row, EncodeRows[row]);
        }

        for (int row = 0; row < UrlRows.Length; row++)
        {
            CheckUrlEncode(checks, row, UrlRows[row]);
        }

        for (int row = 0; row < BadDecodeRows.Length; row++)
        {
            bool decoded = CurlBase64Decoder.TryDecode(Encoding.Latin1.GetBytes(BadDecodeRows[row]), out _);
            checks.FailUnless(!decoded, $"Test {row} URL bad decoded badly. Returned '0', expected '61'");
        }

        return Task.FromResult(checks.Result);
    }

    private static void CheckEncodeAndDecode(UnitChecks checks, int row, (string Input, int Length, string Output) entry)
    {
        byte[] input = Encoding.Latin1.GetBytes(entry.Input)[..entry.Length];
        string encoded = Convert.ToBase64String(input);
        checks.FailUnless(encoded.Length == entry.Output.Length, $"Test {row} wrong output size");
        checks.FailUnless(encoded == entry.Output, $"Test {row} encoded badly");
        bool decodedOk = CurlBase64Decoder.TryDecode(Encoding.ASCII.GetBytes(entry.Output), out byte[] decoded);
        checks.FailUnless(decodedOk, $"Test {row} URL decode returned 61");
        checks.FailUnless(decoded.Length == input.Length, $"Test {row} URL decode output length {decoded.Length} instead of {input.Length}");
        checks.FailUnless(decoded.AsSpan().SequenceEqual(input), $"Test {row} URL decoded badly. Got '{Encoding.Latin1.GetString(decoded)}', expected '{entry.Input}'");
    }

    private static void CheckUrlEncode(UnitChecks checks, int row, (string Input, int Length, string Output) entry)
    {
        string encoded = Base64Url.EncodeToString(Encoding.Latin1.GetBytes(entry.Input)[..entry.Length]);
        checks.WarnUnless(encoded.Length == entry.Output.Length, $"Test {row} URL encoded output length {encoded.Length} instead of {entry.Output.Length}");
        checks.FailUnless(encoded == entry.Output, $"Test {row} URL encoded badly. Got '{encoded}', expected '{entry.Output}'");
    }
}
