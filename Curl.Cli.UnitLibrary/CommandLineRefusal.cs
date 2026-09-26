using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Why the command line was refused: the lines curl prints on standard error,
/// <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c> (or, for <see cref="NoUrlSpecified"/>,
/// <c>curl: (2) no URL specified</c>) followed by <see cref="TryHelpLine"/>, with one more line
/// in front for <see cref="FileDoesNotExist"/>,
/// and the exit code, which is always <see cref="CurlExitCode.FailedInit"/> (curl's
/// <c>CURLE_FAILED_INIT</c>, exit 2; see <see href="https://curl.se/libcurl/c/libcurl-errors.html"/>).
/// It writes nothing itself; the console layer writes the lines and chooses the newline.
/// </summary>
/// <remarks>
/// The message texts were checked byte for byte against the local curl 8.21.0 on 2026-09-26.
/// </remarks>
public sealed class CommandLineRefusal
{
    /// <summary>The second line of every refusal, exactly as curl prints it.</summary>
    public const string TryHelpLine = "curl: try 'curl --help' or 'curl --manual' for more information";

    private CommandLineRefusal(params string[] linesBeforeTryHelp)
    {
        StandardErrorLines = [.. linesBeforeTryHelp, TryHelpLine];
    }

    /// <summary>The exit code curl returns for a refused command line: <see cref="CurlExitCode.FailedInit"/>.</summary>
    public CurlExitCode ExitCode => CurlExitCode.FailedInit;

    /// <summary>
    /// The lines to write to standard error, without line terminators: two, or three for
    /// <see cref="FileDoesNotExist"/>.
    /// </summary>
    public IReadOnlyList<string> StandardErrorLines { get; }

    /// <summary>Refuses an option name that is not in the option table.</summary>
    /// <param name="spelledOption">The whole argument as typed.</param>
    /// <returns>A refusal reading <c>is unknown</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spelledOption"/> is <see langword="null"/>.</exception>
    public static CommandLineRefusal UnknownOption(string spelledOption) =>
        Create(spelledOption, "is unknown");

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
            $"curl: The file '{file}' provided to {longOption} does not exist",
            $"curl: option {spelledOption}: is badly used here");
    }

    private static CommandLineRefusal Create(string spelledOption, string reason)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);

        return new CommandLineRefusal($"curl: option {spelledOption}: {reason}");
    }
}
