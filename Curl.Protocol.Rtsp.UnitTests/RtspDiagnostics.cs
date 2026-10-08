using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Writes what an RTSP test arranges and gets - the URL and options, the scripted reply, the
/// request sent, the transcript of transfer events and the transfer's result with its exit code
/// and error text - as <c>ARRANGE</c>, <c>ACT</c>, <c>BYTES</c>, <c>ASSERT</c> and <c>DIFF</c>
/// lines through the shared <see cref="TestDiagnostics" /> helper.
/// </summary>
internal static class RtspDiagnostics
{
    /// <summary>The most characters of one text value written; the rest are counted.</summary>
    private const int TextCap = 200;

    /// <summary>Writes the transfer's URL and the options that shape its request.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="context">The transfer's context.</param>
    public static void ArrangeContext(this TestDiagnostics diagnostics, TransferContext context)
    {
        diagnostics.Arrange("url", context.Url.OriginalString);
        if (context.Http is { } http)
        {
            diagnostics.Arrange("headers", http.Headers.Count == 0 ? "(none)" : Text(string.Join(" | ", http.Headers)));
            diagnostics.Arrange("fail mode", http.Fail);
            if (http.UserAgent is { } agent)
            {
                diagnostics.Arrange("user agent", agent);
            }

            if (http.Referer is { } referer)
            {
                diagnostics.Arrange("referer", referer);
            }

            if (http.CustomMethod is { } method)
            {
                diagnostics.Arrange("custom method", method);
            }
        }

        WriteIfSet(diagnostics, "resume from", context.ResumeFrom);
        WriteIfSet(diagnostics, "range", context.RangeText);
        WriteIfSet(diagnostics, "max file size", context.MaxFileSize);
        if (context.NoBody)
        {
            diagnostics.Arrange("no body", true);
        }

        if (context.Credentials is { } credentials)
        {
            diagnostics.Arrange("credentials", $"user {credentials.UserName.Length} characters, password {credentials.Password.Length} characters");
        }

        if (context.PostData is { } postData)
        {
            diagnostics.Arrange("post data", $"{postData.Length} bytes");
        }
    }

    /// <summary>Writes the reply the scripted server sends, as text and as bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="reply">The reply.</param>
    public static void ArrangeReply(this TestDiagnostics diagnostics, string reply)
    {
        diagnostics.Arrange("scripted reply", reply.Length == 0 ? "(nothing: the server closes)" : Text(reply));
        diagnostics.Bytes("scripted reply", Encoding.Latin1.GetBytes(reply));
    }

    /// <summary>Writes each read the scripted server returns, in order.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="reads">The reads.</param>
    public static void ArrangeReads(this TestDiagnostics diagnostics, IReadOnlyList<byte[]> reads)
    {
        diagnostics.Arrange("scripted reads", $"{reads.Count} reads, {reads.Sum(read => read.Length)} bytes");
        for (int index = 0; index < Math.Min(reads.Count, 8); index++)
        {
            diagnostics.Bytes($"read {index}", reads[index]);
        }
    }

    /// <summary>Writes the transfer's result: exit code, error text, bytes and reply status.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="result">The result.</param>
    public static void ActResult(this TestDiagnostics diagnostics, TransferResult result)
    {
        diagnostics.Act("exit code", ExitCode(result.ExitCode));
        diagnostics.Act("error", result.ErrorMessage is null ? "(none)" : Text(result.ErrorMessage));
        diagnostics.Act("bytes transferred", result.BytesTransferred);
        if (result.Report is { } report)
        {
            diagnostics.Act("reply status", report.ResponseCode);
        }
    }

    /// <summary>Writes the bytes a connection was sent, as text and as bytes.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="sent">The bytes sent.</param>
    public static void ActSent(this TestDiagnostics diagnostics, byte[] sent)
    {
        diagnostics.Act("request sent", sent.Length == 0 ? "(nothing)" : Text(Encoding.Latin1.GetString(sent)));
        diagnostics.Bytes("request sent", sent);
    }

    /// <summary>Writes the transfer-event transcript, one escaped line per event.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="transcript">The transcript.</param>
    public static void ActTranscript(this TestDiagnostics diagnostics, IReadOnlyList<string> transcript) =>
        diagnostics.Act("transcript", Lines(transcript));

    /// <summary>Writes the ASSERT line for the transfer's exit code.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="expected">The expected exit code.</param>
    /// <param name="result">The result.</param>
    public static void AssertExitCode(this TestDiagnostics diagnostics, CurlExitCode expected, TransferResult result) =>
        diagnostics.Assert("exit code", ExitCode(expected), ExitCode(result.ExitCode));

    /// <summary>Writes a DIFF line between expected text and the bytes actually produced.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="actual">The bytes produced, read as Latin-1.</param>
    public static void DiffText(this TestDiagnostics diagnostics, string label, string expected, byte[] actual) =>
        diagnostics.Diff(label, expected, Encoding.Latin1.GetString(actual));

    /// <summary>Writes an ASSERT line between two lists of lines, each escaped.</summary>
    /// <param name="diagnostics">The test's diagnostics.</param>
    /// <param name="label">What is compared.</param>
    /// <param name="expected">The expected lines.</param>
    /// <param name="actual">The actual lines.</param>
    public static void AssertLines(this TestDiagnostics diagnostics, string label, IEnumerable<string> expected, IEnumerable<string> actual) =>
        diagnostics.Assert(label, Lines(expected), Lines(actual));

    /// <summary>Formats an exit code as its name and number.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <returns>For example <c>RtspCseqError (85)</c>.</returns>
    public static string ExitCode(CurlExitCode exitCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{exitCode} ({(int)exitCode})");

    /// <summary>Formats lines as one bracketed, escaped list.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>For example <c>[&lt; CSeq: 1\r\n, * closing connection #0]</c>.</returns>
    public static string Lines(IEnumerable<string> lines) => "[" + string.Join(", ", lines.Select(Text)) + "]";

    /// <summary>Escapes control characters and caps the length, so one value stays on one line.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Text(string text)
    {
        var builder = new StringBuilder();
        foreach (char character in text.Length > TextCap ? text[..TextCap] : text)
        {
            builder.Append(character switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                '\0' => "\\0",
                < ' ' or (char)0x7f => string.Create(CultureInfo.InvariantCulture, $"\\x{(int)character:x2}"),
                _ => character.ToString(),
            });
        }

        return text.Length > TextCap ? $"{builder}... ({text.Length} characters)" : builder.ToString();
    }

    private static void WriteIfSet(TestDiagnostics diagnostics, string label, object? value)
    {
        if (value is not null)
        {
            diagnostics.Arrange(label, value);
        }
    }
}
