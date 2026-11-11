namespace Curl.Extended.Protocol.Plugin;

/// <summary>
/// Builds the <c>cmd.exe</c> arguments that run a <c>.bat</c> or <c>.cmd</c> helper with the URL as one quoted
/// argument no shell character in it can break out of (ADR-1238): <c>&amp; | &lt; &gt; ^</c> stay literal inside the
/// quotes, every <c>%</c> is followed by <c>%cd:~,%</c>, which expands to nothing, so no <c>%NAME%</c> expands, and a
/// value holding a double quote, carriage return, line feed or NUL, which cmd cannot carry, is refused.
/// </summary>
internal static class BatchHelperCommandLine
{
    private const string PercentNeutralised = "%%cd:~,%";

    /// <summary>Tells whether Windows runs <paramref name="program" /> through cmd.exe: its name ends in <c>.bat</c> or <c>.cmd</c>, ignoring case and the trailing dots and spaces Windows drops.</summary>
    /// <param name="program">The helper's path.</param>
    /// <returns><see langword="true" /> for a batch file.</returns>
    public static bool IsBatchFile(string program)
    {
        string extension = Path.GetExtension(program.TrimEnd('.', ' '));
        return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds the arguments of <c>cmd.exe</c> that run <paramref name="program" /> with <paramref name="url" /> as its one argument.</summary>
    /// <param name="program">The batch helper's path.</param>
    /// <param name="url">The URL as typed.</param>
    /// <returns><c>/e:ON /v:OFF /d /c ""program" "url""</c>, each value quoted and its <c>%</c> neutralised.</returns>
    /// <exception cref="IOException">The program or URL holds a double quote, carriage return, line feed or NUL.</exception>
    public static string ArgumentsFor(string program, string url) => $"/e:ON /v:OFF /d /c \"{Quoted(program)} {Quoted(url)}\"";

    private static string Quoted(string value)
    {
        if (value.AsSpan().IndexOfAny("\"\r\n\0") >= 0)
        {
            throw new IOException("a batch helper cannot receive a double quote, carriage return, line feed or NUL");
        }

        return "\"" + value.Replace("%", PercentNeutralised, StringComparison.Ordinal) + "\"";
    }
}
