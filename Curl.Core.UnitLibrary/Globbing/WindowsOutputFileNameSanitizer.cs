namespace Curl.Core.Globbing;

/// <summary>
/// Replaces the characters a Windows file name cannot hold, as curl 8.21.0's
/// <c>sanitize_file_name</c> does with <c>SANITIZE_ALLOW_PATH | SANITIZE_ALLOW_RESERVED</c>,
/// the flags <c>glob_match_url</c> passes on Windows.
/// </summary>
/// <remarks>
/// Every control character from U+0001 to U+001F and each of <c>| &lt; &gt; " ? *</c>
/// becomes <c>_</c>. Everything else stays: <c>/</c>, <c>\</c> and <c>:</c> (paths are
/// allowed), reserved device names such as <c>CON</c>, trailing dots and spaces, U+007F and
/// every character past it. A leading <c>\\?\</c> long-path prefix is kept whole. curl
/// 8.21.0 sets no length limit here: a 40000-character name came back unchanged. Measured
/// on 2026-09-26 against curl 8.21.0 (x86_64-w64-mingw32, Schannel).
/// </remarks>
internal static class WindowsOutputFileNameSanitizer
{
    private const string LongPathPrefix = @"\\?\";

    private const string BannedPunctuation = "|<>\"?*";

    /// <summary>Returns <paramref name="fileName" /> with every character Windows bans replaced by <c>_</c>.</summary>
    /// <param name="fileName">The output file name after <c>#N</c> substitution.</param>
    /// <returns>The sanitized file name.</returns>
    public static string Sanitize(string fileName)
    {
        int start = fileName.StartsWith(LongPathPrefix, StringComparison.Ordinal) ? LongPathPrefix.Length : 0;
        char[] characters = fileName.ToCharArray();
        for (int index = start; index < characters.Length; index++)
        {
            if (IsBanned(characters[index]))
            {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }

    private static bool IsBanned(char character) =>
        char.IsBetween(character, '\u0001', '\u001F') || BannedPunctuation.Contains(character, StringComparison.Ordinal);
}
