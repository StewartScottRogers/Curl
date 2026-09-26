using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Why the command line was refused: the lines curl prints on standard error,
/// <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c> (or, for <see cref="NoUrlSpecified"/>,
/// <c>curl: (2) no URL specified</c>) followed by <see cref="TryHelpLine"/>, or <see cref="TryHelpLine"/>
/// alone for <see cref="EmptyCommandLine"/>, with the error lines curl printed on the way in front
/// (one for <see cref="FileDoesNotExist"/>, <see cref="ContinueAtExclusiveWithRange"/> and
/// <see cref="DataFileUnreadable"/>, any number for a refusal met inside a <c>-K</c> file), and the
/// exit code, which is <see cref="CurlExitCode.FailedInit"/>
/// (curl's <c>CURLE_FAILED_INIT</c>, exit 2; see <see href="https://curl.se/libcurl/c/libcurl-errors.html"/>)
/// for every refusal but a file that cannot be read (<see cref="DataFileUnreadable"/>,
/// <see cref="ConfigFileUnreadable"/>), which is <see cref="CurlExitCode.ReadError"/> (26).
/// It writes nothing itself; the console layer writes the lines and chooses the newline.
/// </summary>
/// <remarks>
/// The message texts were checked byte for byte against the local curl 8.21.0 on 2026-09-26.
/// </remarks>
public sealed class CommandLineRefusal
{
    /// <summary>The last line of every refusal, exactly as curl prints it.</summary>
    public const string TryHelpLine = "curl: try 'curl --help' or 'curl --manual' for more information";

    /// <summary>The reason curl gives for an option it does not know.</summary>
    private const string UnknownOptionReason = "is unknown";

    /// <summary>The reason curl gives for a file it cannot read.</summary>
    private const string ReadErrorReason = "error encountered when reading a file";

    /// <summary>The deepest <c>-K</c> nesting curl 8.21.0 reads: <c>CONFIG_MAX_LEVELS</c>.</summary>
    internal const int MaximumConfigFileDepth = 5;

    private CommandLineRefusal(CurlExitCode exitCode, IReadOnlyList<string> errorLines, string spelledOption, string reason)
    {
        ExitCode = exitCode;
        ErrorLines = errorLines;
        Reason = reason;
        StandardErrorLines = [.. errorLines, $"curl: option {spelledOption}: {reason}", TryHelpLine];
    }

    private CommandLineRefusal(params string[] linesBeforeTryHelp)
    {
        ExitCode = CurlExitCode.FailedInit;
        ErrorLines = linesBeforeTryHelp;
        Reason = string.Empty;
        StandardErrorLines = [.. linesBeforeTryHelp, TryHelpLine];
    }

    /// <summary>
    /// The exit code curl returns for the refused command line: <see cref="CurlExitCode.FailedInit"/>,
    /// or <see cref="CurlExitCode.ReadError"/> when a file could not be read.
    /// </summary>
    public CurlExitCode ExitCode { get; }

    /// <summary>
    /// The lines to write to standard error, without line terminators: two, one for
    /// <see cref="EmptyCommandLine"/>, or three for
    /// <see cref="FileDoesNotExist"/>, and for <see cref="ContinueAtExclusiveWithRange"/> and
    /// <see cref="DataFileUnreadable"/> when errors are not hidden, and more for a refusal met inside
    /// a <c>-K</c> file.
    /// </summary>
    public IReadOnlyList<string> StandardErrorLines { get; }

    /// <summary>
    /// The lines of <see cref="StandardErrorLines"/> curl prints as error messages before the
    /// <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c> line; empty when there are none.
    /// </summary>
    internal IReadOnlyList<string> ErrorLines { get; }

    /// <summary>
    /// The reason in the <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c> line, such as
    /// <c>is unknown</c>; empty for <see cref="NoUrlSpecified"/> and <see cref="EmptyCommandLine"/>,
    /// which have no such line.
    /// </summary>
    internal string Reason { get; }

    /// <summary>Refuses an option name that is not in the option table.</summary>
    /// <param name="spelledOption">The whole argument as typed.</param>
    /// <returns>A refusal reading <c>is unknown</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal UnknownOption(string spelledOption) =>
        Create(spelledOption, UnknownOptionReason);

    /// <summary>
    /// Refuses <c>--no-&lt;name&gt;</c> where the option named is in the table but curl does not let
    /// it be negated: a value option, or a flag that is not a <see cref="CommandLineOption.NegatableFlag"/>.
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, such as <c>--no-output</c> or <c>--no-output=x</c>.</param>
    /// <returns>A refusal reading <c>the given option cannot be reversed with a --no- prefix</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal CannotBeReversed(string spelledOption) =>
        Create(spelledOption, "the given option cannot be reversed with a --no- prefix");

    /// <summary>Refuses an option that takes a value but is the last argument.</summary>
    /// <param name="spelledOption">The whole argument as typed.</param>
    /// <returns>A refusal reading <c>requires parameter</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal RequiresParameter(string spelledOption) =>
        Create(spelledOption, "requires parameter");

    /// <summary>Refuses an empty value, or an empty positional argument, where content is expected.</summary>
    /// <param name="spelledOption">The whole argument as typed; empty for a positional argument.</param>
    /// <returns>A refusal reading <c>blank argument where content is expected</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal BlankArgument(string spelledOption) =>
        Create(spelledOption, "blank argument where content is expected");

    /// <summary>Refuses a numeric option value that is not a well-formed number.</summary>
    /// <param name="spelledOption">The whole argument as typed.</param>
    /// <returns>A refusal reading <c>expected a proper numerical parameter</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal ExpectedProperNumericalParameter(string spelledOption) =>
        Create(spelledOption, "expected a proper numerical parameter");

    /// <summary>Refuses a numeric option value that is negative.</summary>
    /// <param name="spelledOption">The whole argument as typed.</param>
    /// <returns>A refusal reading <c>expected a positive numerical parameter</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal ExpectedPositiveNumericalParameter(string spelledOption) =>
        Create(spelledOption, "expected a positive numerical parameter");

    /// <summary>Refuses a numeric option value that is larger than the option allows.</summary>
    /// <param name="spelledOption">The whole argument as typed.</param>
    /// <returns>A refusal reading <c>too large number</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal TooLargeNumber(string spelledOption) =>
        Create(spelledOption, "too large number");

    /// <summary>Refuses an option value curl reads but cannot use, such as a size in an unknown unit.</summary>
    /// <param name="spelledOption">The whole argument as typed.</param>
    /// <returns>A refusal reading <c>is badly used here</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal BadlyUsedHere(string spelledOption) =>
        Create(spelledOption, "is badly used here");

    /// <summary>
    /// Refuses <c>-C</c>/<c>--continue-at</c> and <c>-r</c>/<c>--range</c> on one command line,
    /// naming whichever came second: <c>curl: --continue-at is mutually exclusive with --range</c>,
    /// <c>curl: option &lt;spelled&gt;: is badly used here</c> and the try-help line.
    /// </summary>
    /// <remarks>
    /// curl 8.21.0 prints the first line as an error message, which <c>-s</c>/<c>--silent</c>
    /// hides unless <c>-S</c>/<c>--show-error</c> is also given, and only the options read before
    /// the refused one count: <c>-s -r 0-4 -C 5</c> prints two lines, <c>-r 0-4 -C 5 -s</c> and
    /// <c>-s -S -r 0-4 -C 5</c> print three. The other two lines are always printed.
    /// </remarks>
    /// <param name="spelledOption">The whole argument as typed, for whichever option came second.</param>
    /// <param name="errorsHidden">
    /// <see langword="true"/> when <c>-s</c> without <c>-S</c> was read before the refused option.
    /// </param>
    /// <returns>A refusal of three lines, or two when <paramref name="errorsHidden"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal ContinueAtExclusiveWithRange(string spelledOption, bool errorsHidden)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);

        return new CommandLineRefusal(
            CurlExitCode.FailedInit,
            errorsHidden ? [] : ["curl: --continue-at is mutually exclusive with --range"],
            spelledOption,
            "is badly used here");
    }

    /// <summary>
    /// Refuses an empty command line with <see cref="TryHelpLine"/> alone, as curl does.
    /// </summary>
    /// <remarks>
    /// Measured with the local curl 8.21.0 on 2026-09-26: <c>curl</c> with no arguments prints only
    /// the try-help line on standard error, no <c>no URL specified</c> line, and exits 2.
    /// </remarks>
    /// <returns>A refusal of one line, <see cref="TryHelpLine"/>.</returns>
    public static CommandLineRefusal EmptyCommandLine() =>
        new();

    /// <summary>Refuses a command line that has arguments but names no URL.</summary>
    /// <returns>A refusal whose first line is <c>curl: (2) no URL specified</c>.</returns>
    public static CommandLineRefusal NoUrlSpecified() =>
        new("curl: (2) no URL specified");

    /// <summary>
    /// Refuses an option whose file does not exist, in curl's three lines:
    /// <c>curl: The file '&lt;file&gt;' provided to &lt;long option&gt; does not exist</c>,
    /// <c>curl: option &lt;spelled&gt;: is badly used here</c> and the try-help line.
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, such as <c>--cacert</c> or <c>--cacert=</c>.</param>
    /// <param name="longOption">The option's long name with its <c>--</c>, as curl names it in the first line.</param>
    /// <param name="file">The value given, possibly empty.</param>
    /// <returns>A three-line refusal.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static CommandLineRefusal FileDoesNotExist(string spelledOption, string longOption, string file)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(longOption);
        ArgumentNullException.ThrowIfNull(file);

        return new CommandLineRefusal(
            CurlExitCode.FailedInit,
            [$"curl: The file '{file}' provided to {longOption} does not exist"],
            spelledOption,
            "is badly used here");
    }

    /// <summary>
    /// Refuses a <c>-d @file</c> / <c>--data @file</c> or <c>-H @file</c> / <c>--header @file</c>
    /// whose file cannot be opened or read, in curl's
    /// three lines: <c>curl: Failed to open &lt;file&gt;</c>,
    /// <c>curl: option &lt;spelled&gt;: error encountered when reading a file</c> and the try-help
    /// line, with exit code <see cref="CurlExitCode.ReadError"/> (26).
    /// </summary>
    /// <remarks>
    /// Measured with the local curl 8.21.0 on 2026-09-26: <c>-s</c> without <c>-S</c>, read before
    /// the <c>-d</c>, hides the first line; <c>-d @</c> names the empty file and prints
    /// <c>curl: Failed to open </c> with its trailing space.
    /// </remarks>
    /// <param name="spelledOption">The whole argument as typed, such as <c>-d</c>, <c>-d@missing</c> or <c>--data=@missing</c>.</param>
    /// <param name="file">The file name after <c>@</c>, possibly empty.</param>
    /// <param name="errorsHidden"><see langword="true"/> when <c>-s</c> without <c>-S</c> was read before the refused option.</param>
    /// <returns>A refusal of three lines, or two when <paramref name="errorsHidden"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> or <paramref name="file"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal DataFileUnreadable(string spelledOption, string file, bool errorsHidden)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);
        ArgumentNullException.ThrowIfNull(file);

        return new CommandLineRefusal(
            CurlExitCode.ReadError,
            errorsHidden ? [] : [$"curl: Failed to open {file}"],
            spelledOption,
            ReadErrorReason);
    }

    /// <summary>
    /// Refuses a <c>-K</c> / <c>--config</c> file that cannot be opened or read, in curl's three lines:
    /// <c>curl: cannot read config from '&lt;file&gt;'</c> (wrapped at 79 columns as curl wraps it,
    /// and hidden when <paramref name="errorsHidden"/>),
    /// <c>curl: option &lt;spelled&gt;: error encountered when reading a file</c> and the try-help line,
    /// with exit code <see cref="CurlExitCode.ReadError"/> (26).
    /// </summary>
    /// <remarks>
    /// Measured with the local curl 8.21.0 on 2026-09-26: <c>-K nx.cfg</c>, <c>-K ''</c> and
    /// <c>--config=</c> (which names the empty file) each exit 26 with these lines; <c>-s -K nx.cfg</c>
    /// drops the first, <c>-s -S -K nx.cfg</c> keeps it.
    /// </remarks>
    /// <param name="spelledOption">The whole argument as typed, such as <c>-K</c> or <c>--config=</c>, or the option as written in an enclosing file.</param>
    /// <param name="file">The file name as given, possibly empty.</param>
    /// <param name="errorsHidden"><see langword="true"/> when <c>-s</c> without <c>-S</c> was read before the refused option.</param>
    /// <returns>A refusal of three lines or more, or two when <paramref name="errorsHidden"/>.</returns>
    internal static CommandLineRefusal ConfigFileUnreadable(string spelledOption, string file, bool errorsHidden) =>
        new(
            CurlExitCode.ReadError,
            ErrorMessageLines(errorsHidden, CannotReadConfigMessage(file)),
            spelledOption,
            ReadErrorReason);

    /// <summary>
    /// Refuses a <c>-K</c> / <c>--config</c> read while <see cref="MaximumConfigFileDepth"/> files are
    /// already open: <c>curl: Max config file recursion level reached (5)</c> (hidden when
    /// <paramref name="errorsHidden"/>), <c>curl: option &lt;spelled&gt;: is badly used here</c> and the
    /// try-help line. Each enclosing file then adds its own line (see <see cref="ConfigFileOptionRefused"/>).
    /// </summary>
    /// <param name="spelledOption">The option as written in the file.</param>
    /// <param name="errorsHidden"><see langword="true"/> when <c>-s</c> without <c>-S</c> is in effect.</param>
    /// <returns>The refusal.</returns>
    internal static CommandLineRefusal ConfigFileTooDeep(string spelledOption, bool errorsHidden) =>
        new(
            CurlExitCode.FailedInit,
            ErrorMessageLines(errorsHidden, $"Max config file recursion level reached ({MaximumConfigFileDepth})"),
            spelledOption,
            "is badly used here");

    /// <summary>
    /// Refuses a line of a <c>-K</c> file whose parameter its option did not use (a flag given a
    /// parameter, as in <c>silent foo</c>), with the reason <c>had unsupported trailing garbage</c>.
    /// Only its reason and exit code are ever shown, through <see cref="ConfigFileOptionRefused"/>.
    /// </summary>
    /// <param name="spelledOption">The option as written in the file.</param>
    /// <returns>The refusal.</returns>
    internal static CommandLineRefusal UnusedConfigFileParameter(string spelledOption) =>
        Create(spelledOption, "had unsupported trailing garbage");

    /// <summary>
    /// Refuses the <c>-K</c> / <c>--config</c> option <paramref name="spelledOption"/> because line
    /// <paramref name="lineNumber"/> of its file was refused with <paramref name="lineRefusal"/>, as curl
    /// 8.21.0 reports it: the line refusal's own error lines, then
    /// <c>curl: &lt;file&gt;:&lt;line&gt; config file option '&lt;option&gt;' &lt;reason&gt;</c>, then, when
    /// the line failed to read a file, <c>curl: cannot read config from '&lt;file&gt;'</c>, then
    /// <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c> and the try-help line. The reason is the line
    /// refusal's, except that an unknown option becomes <c>found an unknown config option</c>; the exit
    /// code is the line refusal's. The two error lines are wrapped at 79 columns as curl wraps them,
    /// and hidden when <paramref name="errorsHidden"/>.
    /// </summary>
    /// <param name="spelledOption">The <c>-K</c> option as typed, or as written in the enclosing file.</param>
    /// <param name="file">The file name as curl shows it: as given, or <c>&lt;stdin&gt;</c> for <c>-</c>.</param>
    /// <param name="lineNumber">The line's number, counting only lines that are neither blank nor comments, as curl counts.</param>
    /// <param name="option">The option as written on the line.</param>
    /// <param name="lineRefusal">Why the line was refused.</param>
    /// <param name="errorsHidden"><see langword="true"/> when <c>-s</c> without <c>-S</c> is in effect after the line.</param>
    /// <returns>The refusal.</returns>
    internal static CommandLineRefusal ConfigFileOptionRefused(string spelledOption, string file, int lineNumber, string option, CommandLineRefusal lineRefusal, bool errorsHidden)
    {
        IReadOnlyList<string> readErrorLines = lineRefusal.ExitCode == CurlExitCode.ReadError
            ? ErrorMessageLines(errorsHidden, CannotReadConfigMessage(file))
            : [];
        return new(
            lineRefusal.ExitCode,
            [
                .. lineRefusal.ErrorLines,
                .. ErrorMessageLines(errorsHidden, $"{file}:{lineNumber} config file option '{option}' {lineRefusal.Reason}"),
                .. readErrorLines,
            ],
            spelledOption,
            lineRefusal.Reason == UnknownOptionReason ? "found an unknown config option" : lineRefusal.Reason);
    }

    private static string CannotReadConfigMessage(string file) => $"cannot read config from '{file}'";

    /// <summary>An error message's lines as curl prints them, or none when errors are hidden.</summary>
    private static IReadOnlyList<string> ErrorMessageLines(bool errorsHidden, string message) =>
        errorsHidden ? [] : WrappedMessage.Lines("curl: ", message);

    private static CommandLineRefusal Create(string spelledOption, string reason)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);

        return new CommandLineRefusal(CurlExitCode.FailedInit, [], spelledOption, reason);
    }
}
