namespace Curl.Console;

/// <summary>
/// Thrown while an option group's dispatch is built when two of its extension options each claim the
/// transfer's connection (ADR-1010). The runner reports it as a command-line error: <c>curl: </c>,
/// <see cref="Exception.Message" />, the try-help line, and exit 2.
/// </summary>
internal sealed class ExtensionOptionConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ExtensionOptionConflictException" /> class.</summary>
    /// <param name="message">What conflicts, naming each option.</param>
    public ExtensionOptionConflictException(string message)
        : base(message)
    {
    }
}
