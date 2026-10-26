namespace Curl.Protocol.Abstractions;

/// <summary>
/// Marks the bytes 0x80 to 0xFF of header text, held one byte per character (Latin-1), so
/// that text carried on from a header - an absolute <c>Location</c> in
/// <see cref="TransferReport.RedirectUrl" /> and the <see cref="TransferReport.EffectiveUrl" />
/// that follows it - is written back as those bytes, as curl writes them, and not as UTF-8
/// (CGR-062, ADR-0824). Byte <c>b</c> is carried as the private-use character
/// <c>U+F700 + b</c> (<c>U+F780</c> to <c>U+F7FF</c>), the marking <c>Curl.Output</c>'s
/// write-out renderer writes as the raw byte.
/// </summary>
public static class HeaderByteText
{
    private const char FirstHighByte = '\u0080';

    private const char LastHighByte = 'ÿ';

    private const char FirstMarkedByte = '';

    private const char LastMarkedByte = '';

    private const int MarkOffset = 0xF700;

    /// <summary>The header text with each character from <c>U+0080</c> to <c>U+00FF</c> marked as the byte it is.</summary>
    /// <param name="headerText">The header text, one byte per character.</param>
    /// <returns>The marked text; every other character is kept.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headerText" /> is <see langword="null" />.</exception>
    public static string Mark(string headerText)
    {
        ArgumentNullException.ThrowIfNull(headerText);
        return Shifted(headerText, FirstHighByte, LastHighByte, MarkOffset);
    }

    /// <summary>The text with each marked byte back as its Latin-1 character, undoing <see cref="Mark" />.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The unmarked text; every other character is kept.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
    public static string Unmark(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Shifted(text, FirstMarkedByte, LastMarkedByte, -MarkOffset);
    }

    /// <summary>Whether <paramref name="text" /> holds a byte marked by <see cref="Mark" />.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true" /> when it holds one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
    public static bool HasMarks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.AsSpan().IndexOfAnyInRange(FirstMarkedByte, LastMarkedByte) >= 0;
    }

    private static string Shifted(string text, char first, char last, int offset) =>
        text.AsSpan().IndexOfAnyInRange(first, last) < 0
            ? text
            : string.Create(text.Length, (text, first, last, offset), static (shifted, state) =>
            {
                for (int index = 0; index < state.text.Length; index++)
                {
                    char character = state.text[index];
                    shifted[index] = character >= state.first && character <= state.last ? (char)(character + state.offset) : character;
                }
            });
}
