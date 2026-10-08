using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Pop3.Fakes;

/// <summary>
/// Writes this project's POP3 inputs and results as <see cref="TestDiagnostics" /> lines
/// (BL-1480): the URL, the scripted replies and the security level a run starts from, and
/// the result, the commands sent and the bytes written that it ends with.
/// </summary>
internal static class Pop3Diagnostics
{
    /// <summary>The most characters <see cref="Show" /> keeps before saying how many more there are.</summary>
    private const int TextDisplayCap = 200;

    /// <summary>
    /// Gets <paramref name="text" /> on one line: CR, LF and NUL as <c>\r</c>, <c>\n</c> and
    /// <c>\0</c>, cut to <see cref="TextDisplayCap" /> characters.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The text to write.</returns>
    public static string Show(string? text)
    {
        if (text is null)
        {
            return "(null)";
        }

        string shown = text.Length > TextDisplayCap ? text[..TextDisplayCap] : text;
        string more = text.Length > TextDisplayCap
            ? string.Create(CultureInfo.InvariantCulture, $" ... ({text.Length - TextDisplayCap} more characters)")
            : string.Empty;
        return "\"" + shown.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\0", "\\0", StringComparison.Ordinal) + "\"" + more;
    }

    /// <summary>Writes the ARRANGE lines of a run against scripted replies.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="url">The URL.</param>
    /// <param name="replies">Everything the server sends.</param>
    /// <param name="sslLevel">The security level asked for.</param>
    public static void ArrangeRun(this TestDiagnostics diagnostics, string url, string replies, TransportSecurityLevel sslLevel = TransportSecurityLevel.None)
    {
        diagnostics.Arrange("url", url);
        diagnostics.Arrange("server replies", Show(replies));
        diagnostics.Arrange("ssl level", sslLevel);
    }

    /// <summary>Writes the ACT lines of a finished run: its result, what it sent and what it wrote.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="run">The run.</param>
    public static void ActRun(this TestDiagnostics diagnostics, Pop3Run run)
    {
        diagnostics.ActResult(run.Result);
        diagnostics.Act("sent", Show(run.Sent));
        diagnostics.Act("connect targets", string.Join(", ", run.Connector.Targets.Select(target => $"{target.Host}:{target.Port} tls {target.UseTls}")));
        diagnostics.Act("handshakes", string.Join(", ", run.Tls.Handshakes.Select(handshake => handshake.TargetHost)));
        diagnostics.Bytes("output", run.Output);
    }

    /// <summary>
    /// Writes the ACT lines of a transfer that reported to <paramref name="events" />: its
    /// result, the verbose transcript and the info lines.
    /// </summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The transfer's result.</param>
    /// <param name="events">The events it reported to.</param>
    /// <param name="sent">Every byte it wrote to the connection.</param>
    public static void ActTransfer(this TestDiagnostics diagnostics, TransferResult result, RecordingTransferEvents events, byte[] sent)
    {
        diagnostics.ActResult(result);
        diagnostics.Act("sent", Show(Encoding.Latin1.GetString(sent)));
        diagnostics.Act("transcript", string.Join(" | ", events.Transcript.Select(Show)));
        diagnostics.Act("info", string.Join(" | ", events.Info.Select(Show)));
    }

    /// <summary>Writes the ACT line of a transfer result: its exit code, bytes and error text.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The result.</param>
    public static void ActResult(this TestDiagnostics diagnostics, TransferResult result) =>
        diagnostics.Act("result", Describe(result));

    /// <summary>
    /// Writes the ASSERT line for an assertion that compares <paramref name="expected" /> with
    /// <paramref name="actual" />, each shown on one line: text through <see cref="Show" />, a
    /// transfer result as its exit code, bytes and error, bytes as hex.
    /// </summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is asserted, usually the actual value's expression.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    public static void AssertValues(this TestDiagnostics diagnostics, string label, object? expected, object? actual) =>
        diagnostics.Assert(label, Describe(expected), Describe(actual));

    /// <summary>Writes the ASSERT line comparing an expected transfer result with the actual one.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected result.</param>
    /// <param name="actual">The actual result.</param>
    public static void AssertResult(this TestDiagnostics diagnostics, TransferResult expected, TransferResult actual) =>
        diagnostics.Assert("result", Describe(expected), Describe(actual));

    private static string Describe(object? value) => value switch
    {
        null => "(null)",
        string text => Show(text),
        TransferResult result => string.Create(
            CultureInfo.InvariantCulture,
            $"exit {(int)result.ExitCode} {result.ExitCode}, {result.BytesTransferred} bytes, error {Show(result.ErrorMessage)}"),
        byte[] bytes => Show(Encoding.Latin1.GetString(bytes)),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
