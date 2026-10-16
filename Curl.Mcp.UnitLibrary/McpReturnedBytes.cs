using System.Text;
using System.Text.Unicode;

namespace Curl.Mcp;

/// <summary>
/// Output bytes as a tool returns them (ADR-0548): text when they are valid UTF-8, otherwise
/// base64, cut to the <see cref="McpResponseCap" /> with the cut stated.
/// </summary>
/// <param name="Content">The returned bytes as text, or as base64 when <paramref name="IsBase64" /> is set.</param>
/// <param name="IsBase64">Whether <paramref name="Content" /> is base64 because the bytes are not valid UTF-8.</param>
/// <param name="TotalBytes">How many bytes the output had.</param>
/// <param name="ReturnedBytes">How many of them <paramref name="Content" /> holds.</param>
public sealed record McpReturnedBytes(string Content, bool IsBase64, int TotalBytes, int ReturnedBytes)
{
    /// <summary>Whether output was cut at the cap.</summary>
    public bool IsTruncated => ReturnedBytes < TotalBytes;

    /// <summary>
    /// The note stating the cut, or <see langword="null" /> when nothing was cut.
    /// </summary>
    public string? TruncationNote => IsTruncated
        ? $"Output truncated: returned {ReturnedBytes} of {TotalBytes} bytes ({McpResponseCap.VariableName} caps it)."
        : null;

    /// <summary>
    /// Encodes <paramref name="output" /> for a tool result, cut to <paramref name="cap" />. Text
    /// is cut before a multi-byte character the cap would split, so it never ends in a broken
    /// sequence; bytes that are not valid UTF-8 are cut at the cap exactly and returned as base64.
    /// </summary>
    /// <param name="output">The output bytes.</param>
    /// <param name="cap">The most bytes to return.</param>
    /// <returns>The returned bytes.</returns>
    public static McpReturnedBytes Encode(ReadOnlySpan<byte> output, McpResponseCap cap)
    {
        int length = Math.Min(output.Length, cap.MaxBytes);
        int textLength = BackOffToCharacterStart(output, length);
        ReadOnlySpan<byte> text = output[..textLength];
        return Utf8.IsValid(text)
            ? new McpReturnedBytes(Encoding.UTF8.GetString(text), false, output.Length, textLength)
            : new McpReturnedBytes(Convert.ToBase64String(output[..length]), true, output.Length, length);
    }

    /// <summary>
    /// Moves a cut at <paramref name="length" /> back past the UTF-8 continuation bytes it would
    /// split from their lead byte, at most three, so the prefix ends on a character boundary.
    /// </summary>
    private static int BackOffToCharacterStart(ReadOnlySpan<byte> output, int length)
    {
        if (length == output.Length)
        {
            return length;
        }

        int start = length;
        while (start > length - 3 && start > 0 && IsContinuationByte(output[start]))
        {
            start--;
        }

        return start;
    }

    private static bool IsContinuationByte(byte value) => (value & 0xC0) == 0x80;
}
