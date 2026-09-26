namespace Curl.Cli;

/// <summary>
/// The warning lines curl prints on standard error about a command line it carries on with:
/// most while reading it, and <see cref="MoreOutputOptionsThanUrls"/> after the transfers. Each
/// is one whole line without a line terminator; the console layer writes it and chooses the
/// newline.
/// </summary>
/// <remarks>
/// The texts were checked byte for byte against the local curl 8.21.0 on 2026-09-26.
/// </remarks>
public static class CommandLineWarning
{
    /// <summary>
    /// The warning for a file name that looks like a flag, which curl still takes as the file
    /// name: <c>Warning: The filename argument '&lt;value&gt;' looks like a flag.</c>
    /// </summary>
    /// <param name="fileName">The value exactly as given.</param>
    /// <returns>The warning line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/>.</exception>
    public static string FileNameLooksLikeFlag(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return $"Warning: The filename argument '{fileName}' looks like a flag.";
    }

    /// <summary>
    /// The two lines curl prints for a <c>-r</c>/<c>--range</c> value that starts with a digit and
    /// has no dash, which it reads as the range from that position to the end. curl wraps the
    /// text at 79 columns, so the first line ends in a space.
    /// </summary>
    public static IReadOnlyList<string> RangeHasNoDash { get; } =
    [
        "Warning: A specified range MUST include at least one dash (-). Appending one ",
        "Warning: for you",
    ];

    /// <summary>
    /// The three lines curl prints for a <c>-r</c>/<c>--range</c> value holding anything but
    /// digits, dashes and commas, which it keeps unchanged. curl wraps the text at 79 columns, so
    /// the first two lines end in a space.
    /// </summary>
    public static IReadOnlyList<string> RangeHasInvalidCharacter { get; } =
    [
        "Warning: Invalid character is found in given range. A specified range MUST ",
        "Warning: have only digits in 'start'-'stop'. The server's response to this ",
        "Warning: request is uncertain.",
    ];

    /// <summary>
    /// The line curl prints when a command line gives more <c>-o</c>/<c>--output</c> values than
    /// URLs: <c>Warning: Got more output options than URLs</c>. curl 8.21.0 prints it once, however
    /// many values are left over, after the last transfer has ended, not while reading the command
    /// line: <c>curl -o f -o g file:///Z:/nx</c> prints <c>curl: (37) Could not open file Z:/nx</c>
    /// and then this line (measured on Windows on 2026-09-26). It is dropped when <c>-s</c> /
    /// <c>--silent</c> is in effect at the end of the command line.
    /// </summary>
    public static string MoreOutputOptionsThanUrls { get; } = "Warning: Got more output options than URLs";
}
