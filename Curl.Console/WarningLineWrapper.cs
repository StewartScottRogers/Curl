namespace Curl.Console;

/// <summary>
/// Wraps a <c>Warning: </c> line exactly as curl 8.21.0's <c>warnf</c> (<c>voutf</c> in
/// <c>src/tool_msgs.c</c>) does, at the terminal width <see cref="TerminalColumns" /> resolves.
/// </summary>
/// <remarks>
/// The text after <see cref="Prefix" /> may take <c>columns - 9</c> characters a line. While
/// the rest is longer than that, it is cut after the last blank (space or tab) at or before
/// index <c>width - 1</c>, or after index <c>width - 1</c> when there is no blank there past
/// index 0, and a cut blank stays at the end of its line. Every piece starts with its own
/// <see cref="Prefix" />. Measured with the local curl 8.21.0 on 2026-09-26.
/// </remarks>
internal static class WarningLineWrapper
{
    /// <summary>The prefix curl writes before each piece of a warning.</summary>
    internal const string Prefix = "Warning: ";

    /// <summary>
    /// Wraps <paramref name="line" /> when it starts with <see cref="Prefix" />; any other
    /// line is returned unchanged, as the only element.
    /// </summary>
    /// <param name="line">A standard-error line, without a terminator.</param>
    /// <param name="columns">The terminal width, from <see cref="TerminalColumns" />.</param>
    /// <returns>The lines to write, in order, without terminators.</returns>
    internal static IReadOnlyList<string> WrapLine(string line, int columns) =>
        line.StartsWith(Prefix, StringComparison.Ordinal) ? WrapText(line[Prefix.Length..], columns) : [line];

    /// <summary>
    /// Wraps the text of a warning, the part after <see cref="Prefix" />, into prefixed lines.
    /// </summary>
    /// <param name="text">The warning text, without <see cref="Prefix" />.</param>
    /// <param name="columns">The terminal width, from <see cref="TerminalColumns" />.</param>
    /// <returns>
    /// Each piece prefixed with <see cref="Prefix" />, without terminators. When
    /// <paramref name="columns" /> is no wider than the prefix the text stays whole.
    /// </returns>
    internal static IReadOnlyList<string> WrapText(string text, int columns)
    {
        int width = columns - Prefix.Length;
        List<string> lines = [];
        int start = 0;

        while (width > 0 && text.Length - start > width)
        {
            int pieceLength = CutIndex(text, start, width) + 1;
            lines.Add(Prefix + text.Substring(start, pieceLength));
            start += pieceLength;
        }

        lines.Add(Prefix + text[start..]);

        return lines;
    }

    /// <summary>
    /// Finds where curl cuts the text that starts at <paramref name="start" />.
    /// </summary>
    /// <param name="text">The warning text.</param>
    /// <param name="start">Where the rest of the text begins.</param>
    /// <param name="width">How many characters of text fit on a line.</param>
    /// <returns>
    /// The index, relative to <paramref name="start" />, of the last blank at or before
    /// <c>width - 1</c>; <c>width - 1</c> when there is none past index 0.
    /// </returns>
    private static int CutIndex(string text, int start, int width)
    {
        int cut = width - 1;
        while (cut > 0 && !IsBlank(text[start + cut]))
        {
            cut--;
        }

        return cut == 0 ? width - 1 : cut;
    }

    /// <summary>Whether <paramref name="character" /> is a blank to curl's <c>ISBLANK</c>: a space or a tab.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true" /> for a space or a tab.</returns>
    private static bool IsBlank(char character) => character is ' ' or '\t';
}
