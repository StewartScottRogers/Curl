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
    /// <summary>How curl 8.21.0 names each <see cref="SelectedHttpMethod"/>, indexed by its value.</summary>
    private static readonly string[] RequestMethodNames =
    [
        string.Empty,
        "GET (-G, --get)",
        "HEAD (-I, --head)",
        "multipart formpost (-F, --form)",
        "POST (-d, --data)",
    ];

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
    /// The warning for a <c>-w @file</c> or <c>-w @-</c> whose file or standard input holds no bytes,
    /// which clears the template: <c>Warning: Failed to read &lt;file&gt;</c>, naming standard input
    /// <c>&lt;stdin&gt;</c>. Measured with <c>curl -w @empty.txt --bogus</c> and
    /// <c>curl -w @- --bogus &lt;/dev/null</c> (curl 8.21.0, Windows, 2026-09-26); a file holding only
    /// a line break or a NUL is not warned about.
    /// </summary>
    /// <param name="fileName">The file name after the <c>@</c>, or <c>&lt;stdin&gt;</c>.</param>
    /// <returns>The warning line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileName"/> is <see langword="null"/>.</exception>
    public static string FailedToRead(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return $"Warning: Failed to read {fileName}";
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
    /// The line curl prints when it cannot read the modification time of the file a
    /// <c>-z</c>/<c>--time-cond</c> value that is not a date names, on Windows for any reason but the
    /// file not existing and elsewhere for any reason; <see cref="TimeConditionIsNotADate"/> follows it.
    /// Measured with <c>curl -z "" -o NUL file:///Z:/.../global.json</c> (curl 8.21.0, Windows, 2026-09-26):
    /// <c>Warning: Failed to get filetime: CreateFile failed: GetLastError 0x00000003</c>; and with
    /// <c>curl -z "" file:///dev/null</c> (curl 8.18.0, OpenSSL, Ubuntu, 2026-09-27):
    /// <c>Warning: Failed to get filetime: No such file or directory</c>.
    /// </summary>
    /// <param name="reason">The failure <see cref="IDataFileReader.TryReadModificationTime"/> reported.</param>
    /// <returns>The warning line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is <see langword="null"/>.</exception>
    public static string FailedToGetFileTime(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return $"Warning: Failed to get filetime: {reason}";
    }

    /// <summary>
    /// The two lines curl prints for a <c>-z</c>/<c>--time-cond</c> value that is neither a date nor a
    /// file whose modification time can be read, after
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
    /// The line curl prints when <c>--fail-with-body</c> replaces an earlier <c>-f</c> / <c>--fail</c>.
    /// Measured with <c>curl -f --fail-with-body http://127.0.0.1:1/</c> (curl 8.21.0, Windows, 2026-09-26).
    /// </summary>
    public static IReadOnlyList<string> FailWithBodyDeselectsFail { get; } =
    [
        "Warning: --fail-with-body deselects --fail here",
    ];

    /// <summary>
    /// The line curl prints when <c>-f</c> / <c>--fail</c> replaces an earlier <c>--fail-with-body</c>;
    /// it names <c>--fail</c> even when <c>-f</c> was typed. Measured with
    /// <c>curl --fail-with-body -f http://127.0.0.1:1/</c> (curl 8.21.0, Windows, 2026-09-26).
    /// </summary>
    public static IReadOnlyList<string> FailDeselectsFailWithBody { get; } =
    [
        "Warning: --fail deselects --fail-with-body here",
    ];

    /// <summary>
    /// The lines curl prints when an option asks for an HTTP request method after another option has
    /// selected a different one: <c>Warning: You can only select one HTTP request method! You asked for
    /// both &lt;requested&gt; and &lt;selected&gt;.</c>, wrapped at 79 columns as curl wraps it, each method
    /// named as curl 8.21.0 names it: <c>GET (-G, --get)</c>, <c>HEAD (-I, --head)</c>,
    /// <c>multipart formpost (-F, --form)</c> or <c>POST (-d, --data)</c>. Measured with
    /// <c>curl --no-head -I</c>, <c>curl -I --no-head</c>, <c>curl -F a=b -I</c>, <c>curl -I -F a=b</c>,
    /// <c>curl --no-head -F a=b</c>, <c>curl -F a=b --no-head</c>, <c>curl -F a=b -d x</c> and
    /// <c>curl -F a=b -d x -G</c> against <c>http://127.0.0.1:1/</c> (curl 8.21.0, Windows, 2026-09-26).
    /// </summary>
    /// <param name="requested">The method the later option asks for.</param>
    /// <param name="selected">The method already selected.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> OnlyOneRequestMethod(SelectedHttpMethod requested, SelectedHttpMethod selected) =>
        WrappedMessage.Lines(
            "Warning: ",
            $"You can only select one HTTP request method! You asked for both {RequestMethodNames[(int)requested]} and {RequestMethodNames[(int)selected]}.");

    /// <summary>
    /// The two lines curl prints, at transfer setup rather than while reading the command line, when
    /// <c>-I</c> / <c>--head</c> selected <c>HEAD</c> and a <c>-d</c> / <c>--data*</c> or <c>--json</c> body
    /// is to be posted (no <c>-G</c>), after which it ends with exit 2. curl wraps the text at 79
    /// columns, so the first line ends in a space. Measured with <c>curl -I -d x http://127.0.0.1:1/</c>
    /// (curl 8.21.0, Windows, 2026-09-27); <c>-d x -I</c> and <c>-I --json x</c> print the same.
    /// </summary>
    public static IReadOnlyList<string> PostRequestedWithHead { get; } =
        OnlyOneRequestMethod(SelectedHttpMethod.Post, SelectedHttpMethod.Head);

    /// <summary>
    /// The two lines curl prints, at transfer setup rather than while reading the command line, when
    /// <c>--no-head</c> selected <c>GET</c> and a <c>-d</c> / <c>--data*</c> or <c>--json</c> body is to be
    /// posted (no <c>-G</c>), after which it ends with exit 2. curl wraps the text at 79 columns, so the
    /// first line ends in a space. Measured with <c>curl --no-head -d x http://127.0.0.1:1/</c>
    /// (curl 8.21.0, Windows, 2026-09-27); <c>-d x --no-head</c> prints the same.
    /// </summary>
    public static IReadOnlyList<string> PostRequestedWithGet { get; } =
        OnlyOneRequestMethod(SelectedHttpMethod.Post, SelectedHttpMethod.Get);

    /// <summary>
    /// The line curl prints when <c>-0</c> / <c>--http1.0</c> or <c>--http1.1</c> asks for a different HTTP
    /// version from the one an earlier of them asked for: <c>Warning: Overrides previous HTTP version option</c>.
    /// Asking for the same version again does not warn. Measured with <c>curl --http1.1 -0</c>,
    /// <c>curl -0 --http1.1 -0</c> (two warnings), <c>curl -0 -0</c> and <c>curl --http1.1 --http1.1</c>
    /// (none) against <c>http://127.0.0.1:1/</c> (curl 8.21.0, Windows, 2026-09-26).
    /// </summary>
    public static IReadOnlyList<string> OverridesPreviousHttpVersion { get; } =
    [
        "Warning: Overrides previous HTTP version option",
    ];

    /// <summary>
    /// The line curl prints when <c>-v</c> / <c>--verbose</c> replaces an earlier <c>--trace</c> or
    /// <c>--trace-ascii</c>: <c>Warning: -v, --verbose overrides an earlier trace option</c>. Measured with
    /// <c>curl --trace t1 -v</c> and <c>curl --trace-ascii t2 -v</c> against <c>http://127.0.0.1:1/</c>
    /// (curl 8.21.0, Windows, 2026-09-26); <c>-v -v</c> does not warn.
    /// </summary>
    public static IReadOnlyList<string> VerboseOverridesTrace { get; } =
    [
        "Warning: -v, --verbose overrides an earlier trace option",
    ];

    /// <summary>
    /// The line curl prints when <c>--trace</c> or <c>--trace-ascii</c> replaces an earlier
    /// <c>-v</c> / <c>--verbose</c> or a trace of the other kind:
    /// <c>Warning: &lt;option&gt; overrides an earlier trace/verbose option</c>. Measured with
    /// <c>curl -v --trace t1</c>, <c>curl -v --trace-ascii t2</c>, <c>curl --trace t1 --trace-ascii t2</c> and
    /// <c>curl --trace-ascii t2 --trace t1</c> against <c>http://127.0.0.1:1/</c> (curl 8.21.0, Windows,
    /// 2026-09-26); <c>--trace t1 --trace t3</c> does not warn.
    /// </summary>
    /// <param name="longName">The option's long name with its <c>--</c>: <c>--trace</c> or <c>--trace-ascii</c>.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> TraceOverridesEarlierTrace(string longName) =>
    [
        $"Warning: {longName} overrides an earlier trace/verbose option",
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
