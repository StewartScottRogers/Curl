namespace Curl.Cli;

/// <summary>
/// The warning lines curl prints on standard error while reading a command line, before it
/// carries on. Each is one whole line without a line terminator; the console layer writes it
/// and chooses the newline.
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
}
