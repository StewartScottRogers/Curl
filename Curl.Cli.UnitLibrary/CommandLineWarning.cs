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
    /// The warning for a <c>-H</c> / <c>--header</c> value holding neither a colon nor a semicolon,
    /// which curl still sends as given: <c>Warning: The provided HTTP header '&lt;value&gt;' does not look like a header?</c>.
    /// Measured with <c>curl -H foo http://127.0.0.1:1/</c> (curl 8.21.0, Windows, 2026-09-26); <c>-H ''</c>
    /// warns with empty quotes, and the lines of a <c>-H @file</c> are never warned about.
    /// </summary>
    /// <param name="header">The value exactly as given.</param>
    /// <returns>The warning line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is <see langword="null"/>.</exception>
    public static string HeaderDoesNotLookLikeAHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        return $"Warning: The provided HTTP header '{header}' does not look like a header?";
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
    /// The two lines curl prints for a <c>-z</c>/<c>--time-cond</c> value that is not a date, after
    /// which it carries on with no time condition. curl wraps the text at 79 columns, so the first
    /// line ends in a space. Measured with <c>curl -z notadate -o NUL file:///Z:/.../global.json</c>
    /// (curl 8.21.0, Windows, 2026-09-26): these two lines, then the transfer, exit 0.
    /// </summary>
    public static IReadOnlyList<string> TimeConditionIsNotADate { get; } =
    [
        "Warning: Illegal date format for -z, --time-cond (and not a filename). ",
        "Warning: Disabling time condition. See curl_getdate(3) for valid date syntax.",
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
