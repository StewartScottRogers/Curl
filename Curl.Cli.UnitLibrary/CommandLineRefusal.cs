using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Why the command line was refused: the two lines curl prints on standard error,
/// <c>curl: option &lt;spelled&gt;: &lt;reason&gt;</c> followed by <see cref="TryHelpLine"/>,
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

    private CommandLineRefusal(string spelledOption, string reason)
    {
        StandardErrorLines = [$"curl: option {spelledOption}: {reason}", TryHelpLine];
    }

    /// <summary>The exit code curl returns for a refused command line: <see cref="CurlExitCode.FailedInit"/>.</summary>
    public CurlExitCode ExitCode => CurlExitCode.FailedInit;

    /// <summary>The two lines to write to standard error, without line terminators.</summary>
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

    private static CommandLineRefusal Create(string spelledOption, string reason)
    {
        ArgumentNullException.ThrowIfNull(spelledOption);

        return new CommandLineRefusal(spelledOption, reason);
    }
}
