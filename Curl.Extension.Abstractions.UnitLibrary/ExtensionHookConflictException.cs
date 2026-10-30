namespace Curl.Extension.Abstractions;

/// <summary>
/// Thrown when two or more extension options (or an extension option and curl's <c>--doh-url</c>) each claim the
/// connection, the curl schemes or the resolver of one transfer. The host reports it as a command-line error.
/// </summary>
public sealed class ExtensionHookConflictException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ExtensionHookConflictException" /> class.</summary>
    /// <param name="message">What conflicts, naming each option.</param>
    public ExtensionHookConflictException(string message)
        : base(message)
    {
    }
}
