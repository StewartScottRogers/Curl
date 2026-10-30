namespace Curl.Extension.PluginProtocol;

/// <summary>
/// Finds the <c>curl-proto-&lt;scheme&gt;</c> programs in the <c>PATH</c> directories, as a shell finds a command
/// (ADR-1142): the first directory holding a scheme's program wins, and on Windows a program's extension must be one
/// of <c>PATHEXT</c>'s.
/// </summary>
/// <param name="path">The <c>PATH</c> variable, or <see langword="null" /> when it is not set.</param>
/// <param name="pathExtensions">The <c>PATHEXT</c> variable, or <see langword="null" /> for Windows' default.</param>
/// <param name="isWindows">Whether <c>PATH</c> is <c>;</c>-separated and a program needs a <c>PATHEXT</c> extension.</param>
internal sealed class HelperProgramSearch(string? path, string? pathExtensions, bool isWindows)
{
    /// <summary>What every helper program's file name starts with.</summary>
    internal const string ProgramPrefix = "curl-proto-";

    private const string DefaultPathExtensions = ".COM;.EXE;.BAT;.CMD";

    private readonly StringComparison fileNameComparison = isWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string[] programExtensions = isWindows ? ListOf(pathExtensions ?? DefaultPathExtensions, ';') : [];

    /// <summary>Gives each scheme a helper serves and the path of its program.</summary>
    /// <param name="listHelperFiles">Lists the paths of one directory's <c>curl-proto-*</c> files.</param>
    /// <param name="reservedSchemes">The schemes curl and the other extensions serve, which no helper takes.</param>
    /// <returns>The programs by lowercase scheme.</returns>
    internal IReadOnlyDictionary<string, string> Find(Func<string, IEnumerable<string>> listHelperFiles, IEnumerable<string> reservedSchemes)
    {
        HashSet<string> reserved = new(reservedSchemes, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> programs = new(StringComparer.Ordinal);
        foreach (string file in ListOf(path ?? string.Empty, isWindows ? ';' : ':').SelectMany(listHelperFiles))
        {
            if (SchemeOf(Path.GetFileName(file)) is { } scheme && !reserved.Contains(scheme))
            {
                programs.TryAdd(scheme, file);
            }
        }

        return programs;
    }

    /// <summary>Gives the scheme a program's file name serves, or <see langword="null" /> when it serves none.</summary>
    /// <param name="fileName">The file name, without its directory.</param>
    /// <returns>The lowercase scheme, or <see langword="null" />.</returns>
    internal string? SchemeOf(string fileName)
    {
        if (!fileName.StartsWith(ProgramPrefix, fileNameComparison))
        {
            return null;
        }

        string scheme = fileName[ProgramPrefix.Length..];
        if (isWindows)
        {
            string extension = Path.GetExtension(scheme);
            if (!programExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            scheme = scheme[..^extension.Length];
        }

        return IsScheme(scheme) ? scheme.ToLowerInvariant() : null;
    }

    // RFC 3986 section 3.1: ALPHA *( ALPHA / DIGIT / "+" / "-" / "." ).
    private static bool IsScheme(string text) =>
        text.Length > 0 && char.IsAsciiLetter(text[0]) && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');

    private static string[] ListOf(string text, char separator) =>
        text.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
