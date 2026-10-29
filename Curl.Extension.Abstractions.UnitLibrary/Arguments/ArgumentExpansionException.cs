using Curl.Protocol.Abstractions;

namespace Curl.Extension.Abstractions.Arguments;

/// <summary>
/// Thrown by an <see cref="IArgumentExpander" /> whose option's value cannot be expanded (ADR-1040). The host ends
/// the run before any transfer: <c>curl: </c>, <see cref="Exception.Message" />, and <see cref="ExitCode" />, such as
/// <see cref="CurlExitCode.ReadError" /> (26) for a file that cannot be read, as <c>-K</c> does, or
/// <see cref="CurlExitCode.FailedInit" /> (2) for a value it cannot make sense of.
/// </summary>
public sealed class ArgumentExpansionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ArgumentExpansionException" /> class.</summary>
    /// <param name="message">What went wrong, printed after <c>curl: </c>.</param>
    /// <param name="exitCode">The exit code the run ends with.</param>
    public ArgumentExpansionException(string message, CurlExitCode exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }

    /// <summary>Gets the exit code the run ends with.</summary>
    public CurlExitCode ExitCode { get; }
}
