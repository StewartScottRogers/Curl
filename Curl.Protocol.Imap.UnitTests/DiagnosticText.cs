using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Imap;

/// <summary>
/// Turns the text an IMAP test compares - commands, tagged replies and transcripts full of
/// CR LF - into one readable line for its <c>ARRANGE</c>, <c>ACT</c> and <c>ASSERT</c> diagnostics.
/// </summary>
internal static class DiagnosticText
{
    /// <summary>Escapes CR, LF and other control characters so the text stays on one line.</summary>
    /// <param name="text">The text, or <see langword="null" />.</param>
    /// <returns>The text quoted, with <c>\r</c>, <c>\n</c> and <c>\xNN</c> escapes, or <c>null</c>.</returns>
    public static string Escape(string? text)
    {
        if (text is null)
        {
            return "null";
        }

        var escaped = new System.Text.StringBuilder("\"");
        foreach (char character in text)
        {
            escaped.Append(character switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                < ' ' or (char)0x7f => $"\\x{(int)character:x2}",
                _ => character.ToString(),
            });
        }

        return escaped.Append('"').ToString();
    }

    /// <summary>Describes a transfer's outcome: its exit code by name and number, its error text and its byte count.</summary>
    /// <param name="result">The transfer's result.</param>
    /// <returns>The description.</returns>
    public static string Result(TransferResult result) =>
        $"{result.ExitCode} ({(int)result.ExitCode}), error {Escape(result.ErrorMessage)}, {result.BytesTransferred} bytes, refused {result.IsConnectionRefused}";

    /// <summary>Escapes each line and joins them in brackets.</summary>
    /// <param name="lines">The lines, such as a transcript.</param>
    /// <returns>The lines as one line.</returns>
    public static string Lines(IEnumerable<string> lines) => "[" + string.Join(", ", lines.Select(Escape)) + "]";
}
