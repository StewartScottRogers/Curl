namespace Curl.Cli;

/// <summary>
/// Standard input as curl's Windows build reads it for <c>-d @-</c>, <c>--data-ascii @-</c>,
/// <c>--json @-</c>, <c>--variable name@-</c>, <c>-H @-</c>, <c>--proxy-header @-</c> and
/// <c>-w @-</c>: in the C runtime's text mode, where a Ctrl-Z byte (0x1A) ends the input and each
/// CR LF pair reads as LF, a CR before any other byte staying. <c>--data-binary @-</c> and
/// <c>--data-urlencode @-</c> switch standard input to binary and read every byte unchanged.
/// Measured with the local curl 8.22.0 on 2026-10-26 (CGR-112, BL-3641): <c>ab</c> Ctrl-Z <c>cd</c>
/// CR LF <c>ef</c> sends <c>ab</c> for <c>-d @-</c>, and <c>a</c> CR LF <c>b</c> CR <c>c</c> sends
/// <c>a</c> LF <c>b</c> CR <c>c</c> for <c>--variable v@-</c>.
/// </summary>
internal static class WindowsTextModeInput
{
    /// <summary>The Ctrl-Z byte a Windows text-mode read takes as the end of the input.</summary>
    private const byte EndOfInput = 0x1A;

    /// <summary>
    /// The bytes curl reads from standard input whose raw bytes are <paramref name="contents"/>: on
    /// Windows those before the first Ctrl-Z with every CR LF turned into LF; elsewhere all of them, unchanged.
    /// </summary>
    /// <param name="contents">The raw bytes of standard input.</param>
    /// <param name="readsInWindowsTextMode">Whether standard input is read as curl's Windows build reads it.</param>
    /// <returns>The bytes curl reads.</returns>
    internal static byte[] Contents(byte[] contents, bool readsInWindowsTextMode)
    {
        if (!readsInWindowsTextMode)
        {
            return contents;
        }

        int endOfInput = Array.IndexOf(contents, EndOfInput);
        int length = endOfInput < 0 ? contents.Length : endOfInput;
        List<byte> read = new(length);
        for (int index = 0; index < length; index++)
        {
            if (!StartsCarriageReturnLineFeed(contents, index, length))
            {
                read.Add(contents[index]);
            }
        }

        return [.. read];
    }

    private static bool StartsCarriageReturnLineFeed(byte[] contents, int index, int length) =>
        contents[index] == (byte)'\r' && index + 1 < length && contents[index + 1] == (byte)'\n';
}
