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
    /// The warning for a <c>--proxy-header</c> value holding neither a colon nor a semicolon,
    /// which curl still sends as given: <c>Warning: The provided proxy header '&lt;value&gt;' does not look like a header?</c>.
    /// Measured with <c>curl --proxy-header bogus http://127.0.0.1:1/</c> and <c>--proxy-header ''</c>
    /// (curl 8.21.0, Windows, 2026-09-27); the lines of a <c>--proxy-header @file</c> are never warned about.
    /// </summary>
    /// <param name="header">The value exactly as given.</param>
    /// <returns>The warning line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is <see langword="null"/>.</exception>
    public static string ProxyHeaderDoesNotLookLikeAHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        return $"Warning: The provided proxy header '{header}' does not look like a header?";
    }

    /// <summary>
    /// The warning curl 8.21.0 prints when <c>-L</c>/<c>--location</c>, <c>--location-trusted</c> or
    /// either's <c>--no-</c> spelling replaces a <c>--follow</c> still in force (measured 2026-10-02,
    /// BL-1223 Notes).
    /// </summary>
    public static IReadOnlyList<string> LocationOverridesFollow { get; } =
    [
        "Warning: --location overrides --follow",
    ];

    /// <summary>
    /// The warning curl 8.21.0 prints when <c>--follow</c> or <c>--no-follow</c> replaces a
    /// <c>-L</c>/<c>--location</c> or <c>--location-trusted</c> still in force (measured 2026-10-02,
    /// BL-1223 Notes).
    /// </summary>
    public static IReadOnlyList<string> FollowOverridesLocation { get; } =
    [
        "Warning: --follow overrides --location",
    ];

    /// <summary>
    /// The warning curl prints for a <c>-r</c>/<c>--range</c> value that starts with a digit and
    /// has no dash, which it reads as the range from that position to the end, as one unwrapped
    /// line; the console layer wraps it at the terminal width as curl does.
    /// </summary>
    public static IReadOnlyList<string> RangeHasNoDash { get; } =
    [
        "Warning: A specified range MUST include at least one dash (-). Appending one for you",
    ];

    /// <summary>
    /// The warning curl prints for a <c>-r</c>/<c>--range</c> value holding anything but digits,
    /// dashes and commas, which it keeps unchanged, as one unwrapped line; the console layer wraps
    /// it at the terminal width as curl does.
    /// </summary>
    public static IReadOnlyList<string> RangeHasInvalidCharacter { get; } =
    [
        "Warning: Invalid character is found in given range. A specified range MUST have only digits in 'start'-'stop'. The server's response to this request is uncertain.",
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
    /// The warning curl prints for a <c>-z</c>/<c>--time-cond</c> value that is neither a date nor a
    /// file whose modification time can be read, after which it carries on with no time condition,
    /// as one unwrapped line; the console layer wraps it at the terminal width as curl does. Measured
    /// with <c>curl -z notadate -o NUL file:///Z:/.../global.json</c> at <c>COLUMNS=200</c>, 79 and 40
    /// (curl 8.21.0, Windows, 2026-09-26 and 2026-09-27): the warning, then the transfer, exit 0.
    /// </summary>
    public static IReadOnlyList<string> TimeConditionIsNotADate { get; } =
    [
        "Warning: Illegal date format for -z, --time-cond (and not a filename). Disabling time condition. See curl_getdate(3) for valid date syntax.",
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
    /// The warning curl prints when an option asks for an HTTP request method after another option has
    /// selected a different one: <c>Warning: You can only select one HTTP request method! You asked for
    /// both &lt;requested&gt; and &lt;selected&gt;.</c>, as one unwrapped line that the console layer wraps
    /// at the terminal width as curl does, each method named as curl 8.21.0 names it:
    /// <c>GET (-G, --get)</c>, <c>HEAD (-I, --head)</c>, <c>multipart formpost (-F, --form)</c> or
    /// <c>POST (-d, --data)</c>. Measured with <c>curl --no-head -I</c>, <c>curl -I --no-head</c>,
    /// <c>curl -F a=b -I</c>, <c>curl -I -F a=b</c>, <c>curl --no-head -F a=b</c>, <c>curl -F a=b --no-head</c>,
    /// <c>curl -F a=b -d x</c> and <c>curl -F a=b -d x -G</c> against <c>http://127.0.0.1:1/</c> (curl 8.21.0,
    /// Windows, 2026-09-26), and <c>curl -F a=b -I --bogus x</c> at <c>COLUMNS=200</c> and 40 (2026-09-27).
    /// </summary>
    /// <param name="requested">The method the later option asks for.</param>
    /// <param name="selected">The method already selected.</param>
    /// <returns>The warning line, as the only element.</returns>
    internal static IReadOnlyList<string> OnlyOneRequestMethod(SelectedHttpMethod requested, SelectedHttpMethod selected) =>
    [
        $"Warning: You can only select one HTTP request method! You asked for both {RequestMethodNames[(int)requested]} and {RequestMethodNames[(int)selected]}.",
    ];

    /// <summary>
    /// The warning curl prints, at transfer setup rather than while reading the command line, when
    /// <c>-I</c> / <c>--head</c> selected <c>HEAD</c> and a <c>-d</c> / <c>--data*</c> or <c>--json</c> body
    /// is to be posted (no <c>-G</c>), after which it ends with exit 2, as one unwrapped line; the console
    /// layer wraps it at the terminal width. Measured with <c>curl -I -d x http://127.0.0.1:1/</c>
    /// (curl 8.21.0, Windows, 2026-09-27) at <c>COLUMNS=200</c>, 79 and 40; <c>-d x -I</c> and
    /// <c>-I --json x</c> print the same.
    /// </summary>
    public static IReadOnlyList<string> PostRequestedWithHead { get; } =
        OnlyOneRequestMethod(SelectedHttpMethod.Post, SelectedHttpMethod.Head);

    /// <summary>
    /// The warning curl prints, at transfer setup rather than while reading the command line, when
    /// <c>--no-head</c> selected <c>GET</c> and a <c>-d</c> / <c>--data*</c> or <c>--json</c> body is to be
    /// posted (no <c>-G</c>), after which it ends with exit 2, as one unwrapped line; the console layer
    /// wraps it at the terminal width. Measured with <c>curl --no-head -d x http://127.0.0.1:1/</c>
    /// (curl 8.21.0, Windows, 2026-09-27) at <c>COLUMNS=200</c>, 79 and 40; <c>-d x --no-head</c> prints the same.
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
    /// The line curl prints for <c>--ssl</c> or <c>--ftp-ssl</c>, which go on in plaintext when the
    /// server refuses TLS: <c>Warning: &lt;option&gt; is an insecure option, consider --ssl-reqd instead</c>.
    /// Measured with <c>curl --ssl</c>, <c>--ftp-ssl</c> and <c>--ssl=x</c> against <c>ftp://127.0.0.1:1/</c>
    /// (curl 8.21.0, Windows, 2026-09-27): the long name only, whatever value is attached; once per
    /// occurrence; none for <c>--no-ssl</c>.
    /// </summary>
    /// <param name="longName">The option's long name with its <c>--</c>: <c>--ssl</c> or <c>--ftp-ssl</c>.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> InsecureSsl(string longName) =>
    [
        $"Warning: {longName} is an insecure option, consider --ssl-reqd instead",
    ];

    /// <summary>
    /// The line curl prints for an option it still accepts but that no longer does anything
    /// (<c>--sslv2</c>, <c>--metalink</c>, <c>--krb4</c> and the rest of its no-function options):
    /// <c>Warning: --&lt;name&gt; is deprecated and has no function anymore</c>, naming the long option
    /// whatever spelling was typed. Measured with each of the nine, <c>-2</c>, <c>-3</c> and
    /// <c>--no-metalink</c> against a loopback 200 (curl 8.21.0, Windows, 2026-09-28): the warning, then
    /// the transfer, exit 0; nothing when <c>-s</c> came first.
    /// </summary>
    /// <param name="longName">The option's long name without its <c>--</c>.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> DeprecatedWithNoFunction(string longName) =>
    [
        $"Warning: --{longName} is deprecated and has no function anymore",
    ];

    /// <summary>
    /// The lines curl prints for a <c>--ftp-method</c> value that is none of <c>multicwd</c>, <c>nocwd</c>
    /// and <c>singlecwd</c> (in any case), after which it uses <c>multicwd</c>:
    /// <c>Warning: unrecognized ftp file method '&lt;value&gt;', using default</c>, wrapped at 79 columns as
    /// curl wraps it. Measured with <c>curl --ftp-method bogus http://127.0.0.1:1/</c>, <c>--ftp-method ''</c>
    /// and a value long enough to wrap (curl 8.21.0, Windows, 2026-09-27): the warning, then the transfer.
    /// </summary>
    /// <param name="value">The value exactly as given.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> UnrecognizedFtpFileMethod(string value) =>
        WrappedMessage.Lines("Warning: ", $"unrecognized ftp file method '{value}', using default");

    /// <summary>
    /// The lines curl prints for a <c>--ftp-ssl-ccc-mode</c> value that is neither <c>active</c> nor
    /// <c>passive</c> (in any case), after which it uses <c>passive</c>:
    /// <c>Warning: unrecognized ftp CCC method '&lt;value&gt;', using default</c>, wrapped at 79 columns as
    /// curl wraps its warnings. Measured with <c>--ftp-ssl-ccc-mode bogus</c>, <c>''</c> and a long value
    /// against an FTP loopback (curl 8.21.0, Windows, 2026-09-28, BL-634 Notes): the warning, then the transfer.
    /// </summary>
    /// <param name="value">The value exactly as given.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> UnrecognizedFtpCccMethod(string value) =>
        WrappedMessage.Lines("Warning: ", $"unrecognized ftp CCC method '{value}', using default");

    /// <summary>
    /// The lines curl prints for a <c>--delegation</c> value that is none of <c>none</c>, <c>policy</c> and
    /// <c>always</c> (in any case), after which it uses <c>none</c>:
    /// <c>Warning: unrecognized delegation method '&lt;value&gt;', using none</c>, wrapped at 79 columns as
    /// curl wraps its warnings. Measured with <c>--delegation bogus</c> and <c>--delegation ''</c> against an
    /// FTP loopback (curl 8.21.0, Windows, and curl 8.18.0, Linux, 2026-09-28, BL-630 Notes): the warning,
    /// then the transfer.
    /// </summary>
    /// <param name="value">The value exactly as given.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> UnrecognizedDelegationMethod(string value) =>
        WrappedMessage.Lines("Warning: ", $"unrecognized delegation method '{value}', using none");

    /// <summary>
    /// The lines curl prints for an <c>--ech ecl:@&lt;file&gt;</c> whose file cannot be read, before refusing the
    /// option as badly used: <c>Warning: Could not read file "&lt;file&gt;" specified for "--ech ecl:" option</c>,
    /// wrapped at 79 columns as curl wraps its warnings. Taken from curl 8.21.0's <c>parse_ech</c>
    /// (<c>src/tool_getparam.c</c>, tag <c>curl-8_21_0</c>); no measured build has ECH (ADR-0151).
    /// </summary>
    /// <param name="file">The file name after the <c>@</c>.</param>
    /// <returns>The warning's lines.</returns>
    internal static IReadOnlyList<string> EchConfigListFileUnreadable(string file) =>
        WrappedMessage.Lines("Warning: ", $"Could not read file \"{file}\" specified for \"--ech ecl:\" option");

    /// <summary>
    /// The line curl prints for a <c>--proto</c> or <c>--proto-redir</c> item naming a scheme it does not know:
    /// <c>Warning: unrecognized protocol '&lt;name&gt;'</c>, the name without its <c>+</c>, <c>-</c> or <c>=</c>
    /// and cut to its first 31 characters by the caller. Measured with <c>curl --proto http,bogus</c> and
    /// <c>--proto-redir http,bogus</c> (curl 8.21.0, Windows, 2026-09-28): the warning, then the transfer.
    /// </summary>
    /// <param name="name">The name as curl repeats it.</param>
    /// <returns>The warning line.</returns>
    internal static string UnrecognizedProtocol(string name) =>
        $"Warning: unrecognized protocol '{name}'";

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
