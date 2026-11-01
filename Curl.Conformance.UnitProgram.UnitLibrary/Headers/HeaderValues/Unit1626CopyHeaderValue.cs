using Curl.Original.Protocol.Http;

namespace Curl.Conformance.UnitProgram.Headers.HeaderValues;

/// <summary>
/// The port of curl 8.22.0's <c>tests/unit/unit1626.c</c>: <c>Curl_copy_header_value</c>, here
/// <see cref="HttpHeaderLine.CopyValue" />, over the C program's 28 rows.
/// </summary>
/// <param name="rows">The rows to check; <see cref="UpstreamRows" /> for the port itself.</param>
public sealed class Unit1626CopyHeaderValue(IReadOnlyList<(string In, string Out)> rows) : InvokeCountingUnitProgram<(string In, string Out)>(rows)
{
    /// <inheritdoc />
    public override string ProgramName => "unit1626";

    /// <summary>The C program's <c>list[]</c>: header line and the value it copies.</summary>
    public static IReadOnlyList<(string In, string Out)> UpstreamRows { get; } =
    [
        ("Header: value", "value"),
        ("Header:value", "value"),
        ("Header:    value", "value"),
        ("Header: \tvalue", "value"),
        ("Header: value  ", "value"),
        ("Header:    value   ", "value"),
        ("Header:", ""),
        ("Header:  ", ""),
        ("Header: \t ", ""),
        ("Header: one two", "one two"),
        ("Header: one two a b c ", "one two a b c"),
        ("Header: text/html", "text/html"),
        ("Header: value\r", "value"),
        ("Header: value\n", "value"),
        ("Header: \"value\"\n", "\"value\""),
        ("Header: \"value\"  ", "\"value\""),
        (" Header: value", "value"),
        ("Header\t: value", "value"),
        ("Header:\t  value", "value"),
        ("Header: foo:bar", "foo:bar"),
        ("Header: foo:bar:baz", "foo:bar:baz"),
        ("Header:\t\t", ""),
        ("Header: value\r\n", "value"),
        ("Header: foo\tbar", "foo\tbar"),
        ("Header:\tfoo", "foo"),
        ("Header : value", "value"),
        ("Header    : value", "value"),
        ("Header :    value", "value"),
    ];

    /// <inheritdoc />
    protected override string? Mismatch((string In, string Out) row)
    {
        string get = HttpHeaderLine.CopyValue(row.In);
        return get == row.Out ? null : $"Input: {row.In}\nGot: {get}\nExpected: {row.Out}\n";
    }
}
