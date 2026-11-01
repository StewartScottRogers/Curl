using Curl.Conformance.UnitProgram.Hashes;
using Curl.Original.Protocol.Http;

namespace Curl.Conformance.UnitProgram.Headers.HeaderValues;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1625.c</c>: <c>Curl_compareheader</c>, here
/// <see cref="HttpHeaderLine.ListsToken" />, over the C program's 33 rows, then a token after a value of
/// <c>MAX_HTTP_RESP_HEADER_SIZE</c> (300 KiB) bytes.
/// </summary>
/// <param name="rows">The rows to check; <see cref="UpstreamRows" /> for the port itself.</param>
public sealed class Unit1625CompareHeader(IReadOnlyList<(string In, string Header, string Extract, bool Expect)> rows)
    : InvokeCountingUnitProgram<(string In, string Header, string Extract, bool Expect)>(rows)
{
    /// <summary>curl's <c>MAX_HTTP_RESP_HEADER_SIZE</c>, the length of the large row's value.</summary>
    public const int MaximumResponseHeaderSize = 300 * 1024;

    /// <inheritdoc />
    public override string ProgramName => "unit1625";

    /// <summary>The C program's <c>list[]</c>: header line, header name, token looked for, and whether it is found.</summary>
    public static IReadOnlyList<(string In, string Header, string Extract, bool Expect)> UpstreamRows { get; } =
    [
        ("Encoding: gzip, chunked", "Encoding:", "chunked", true),
        ("Encoding: chunked", "Encoding:", "chunked", true),
        ("Encoding: a, b, chunked", "Encoding:", "chunked", true),
        ("Encoding: a, b, c, chunked", "Encoding:", "chunked", true),
        ("Encoding: a, chunked, ninja", "Encoding:", "chunked", true),
        ("Encoding:", "Encoding:", "chunked", false),
        ("Encoding:   ", "Encoding:", "chunked", false),
        ("Encoding: ab,cd,ef,gh,ig,kl", "Encoding:", "kl", true),
        ("Encoding: ab,,kl", "Encoding:", "kl", true),
        ("Encoding: ab,cd,,,,kl", "Encoding:", "kl", true),
        ("Encoding: chunked, a, b, c", "Encoding:", "chunked", true),
        ("Encoding: gzip, chunked", "Encoding:", "CHUNKED", true),
        ("Encoding: gzip, CHUNKED", "Encoding:", "chunked", true),
        ("encoDING: gzip, CHUNKED", "encoding:", "chunked", true),
        ("ENCODING: gzip, chunked", "encoding:", "chunked", true),
        ("Encoding: gzip, chunked", "Encoding:", "gzip", true),
        ("Encoding: gzip,chunked", "Encoding:", "gzip", true),
        ("Encoding:    gzip,     chunked", "Encoding:", "chunked", true),
        ("Encoding: gzip, \tchunked", "Encoding:", "chunked", true),
        ("Encoding: gzip\r\n", "Encoding:", "gzip", true),
        ("Encoding: gzip\n", "Encoding:", "gzip", true),
        ("Encoding: gzip\t", "Encoding:", "gzip", true),
        ("Encoding: gzip \n", "Encoding:", "gzip", true),
        ("Encoding: gzip, chunked", "Encoding:", "br", false),
        ("Encoding: gzip, chunked", "Encodin:", "chunked", false),
        ("Encoding: gzip2, chunked", "Encoding:", "gzip", false),
        ("Encoding: gzipp, chunked", "Encoding:", "gzip", false),
        ("Encoding: agzip, chunked", "Encoding:", "gzip", false),
        ("Decoding: gzip, chunked", "Encoding:", "gzip", false),
        ("Encoding: super-nice", "Encoding:", "super-nice", true),
        ("Encoding: extra-good, super-nice", "Encoding:", "super-nice", true),
        ("Encoding: gzipped,  ", "Encoding:", "gzip", false),
        ("Encoding: gzipped,  \r\nEncoding: gzip, chunked", "Encoding:", "chunked", false),
    ];

    /// <inheritdoc />
    protected override string? Mismatch((string In, string Header, string Extract, bool Expect) row)
    {
        bool check = HttpHeaderLine.ListsToken(row.In, row.Header, row.Extract);
        return check == row.Expect
            ? null
            : $"Input: {row.In}\nHeader: {row.Header}\nLook for: {row.Extract}\nReturned: {(check ? "TRUE" : "FALSE")}\n";
    }

    /// <inheritdoc />
    protected override void CheckAfterRows(UnitChecks checks)
    {
        ArgumentNullException.ThrowIfNull(checks);
        string large = $"Encoding: {new string('a', MaximumResponseHeaderSize)}, chunked";
        checks.FailUnless(HttpHeaderLine.ListsToken(large, "Encoding:", "chunked"), "token after 300 KiB was not found");
    }
}
