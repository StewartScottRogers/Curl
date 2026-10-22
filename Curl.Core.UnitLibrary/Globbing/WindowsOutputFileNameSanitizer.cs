namespace Curl.Core.Globbing;

/// <summary>
/// Replaces the characters a Windows file name cannot hold, as curl's
/// <c>sanitize_file_name</c> does: <see cref="Sanitize(string)" /> with
/// <c>SANITIZE_ALLOW_PATH | SANITIZE_ALLOW_RESERVED</c>, the flags <c>glob_match_url</c>
/// passes on Windows, and <see cref="SanitizeFileName(string?, SanitizeFlags, out string?)" />
/// with any flags, as curl 8.22.0's <c>tests/tunit/tool1604.c</c> drives it.
/// </summary>
/// <remarks>
/// Every control character from U+0001 to U+001F and each of <c>| &lt; &gt; " ? *</c>
/// becomes <c>_</c>. With both flags everything else stays: <c>/</c>, <c>\</c> and <c>:</c>
/// (paths are allowed), reserved device names such as <c>CON</c>, trailing dots and spaces,
/// U+007F and every character past it. A leading <c>\\?\</c> long-path prefix is kept whole.
/// curl 8.21.0 sets no length limit here: a 40000-character name came back unchanged.
/// Measured on 2026-09-26 against curl 8.21.0 (x86_64-w64-mingw32, Schannel). The MSDOS-only
/// steps (<c>msdosify</c>, the device <c>stat</c>) are not ported: no Windows build has them.
/// </remarks>
public static class WindowsOutputFileNameSanitizer
{
    /// <summary>The flags of curl's <c>sanitize_file_name</c> (<c>src/tool_doswin.c</c>).</summary>
    [Flags]
    public enum SanitizeFlags
    {
        /// <summary>No flag: path separators and colons are replaced, trailing dots and spaces cut, and reserved device names renamed.</summary>
        None = 0,

        /// <summary><c>SANITIZE_ALLOW_PATH</c>: <c>/</c>, <c>\</c> and <c>:</c> are kept, as are trailing dots and spaces and a leading <c>\\?\</c>.</summary>
        AllowPath = 1,

        /// <summary><c>SANITIZE_ALLOW_RESERVED</c>: reserved device names such as <c>COM1</c> are kept.</summary>
        AllowReserved = 2,
    }

    /// <summary>The results of curl's <c>sanitize_file_name</c> that a Windows build can return.</summary>
    public enum SanitizeResult
    {
        /// <summary><c>SANITIZE_ERR_OK</c> (0): the name was sanitized.</summary>
        Ok = 0,

        /// <summary><c>SANITIZE_ERR_BAD_ARGUMENT</c> (2): there was no name to sanitize.</summary>
        BadArgument = 2,
    }

    private const string LongPathPrefix = @"\\?\";

    private const string BannedPunctuation = "|<>\"?*";

    private const string PathPunctuation = @":/\";

    /// <summary>Returns <paramref name="fileName" /> with every character Windows bans replaced by <c>_</c>.</summary>
    /// <param name="fileName">The output file name after <c>#N</c> substitution.</param>
    /// <returns>The sanitized file name.</returns>
    public static string Sanitize(string fileName)
    {
        SanitizeFileName(fileName, SanitizeFlags.AllowPath | SanitizeFlags.AllowReserved, out string? sanitized);
        return sanitized!;
    }

    /// <summary>Sanitizes <paramref name="fileName" /> as curl's <c>sanitize_file_name</c> does with <paramref name="flags" />.</summary>
    /// <param name="fileName">The file or path name, or <see langword="null" />.</param>
    /// <param name="flags">Which of <c>SANITIZE_ALLOW_PATH</c> and <c>SANITIZE_ALLOW_RESERVED</c> are set.</param>
    /// <param name="sanitized">The sanitized name, or <see langword="null" /> when the result is not <see cref="SanitizeResult.Ok" />.</param>
    /// <returns><see cref="SanitizeResult.BadArgument" /> for a <see langword="null" /> name, otherwise <see cref="SanitizeResult.Ok" />.</returns>
    public static SanitizeResult SanitizeFileName(string? fileName, SanitizeFlags flags, out string? sanitized)
    {
        sanitized = null;
        if (fileName is null)
        {
            return SanitizeResult.BadArgument;
        }

        bool allowPath = flags.HasFlag(SanitizeFlags.AllowPath);
        string replaced = ReplaceBannedCharacters(fileName, allowPath);
        string clipped = allowPath ? replaced : ClipTrailingDotsAndSpaces(replaced);
        sanitized = flags.HasFlag(SanitizeFlags.AllowReserved) ? clipped : RenameReservedDeviceNames(clipped, allowPath);
        return SanitizeResult.Ok;
    }

    private static string ReplaceBannedCharacters(string fileName, bool allowPath)
    {
        int start = allowPath && fileName.StartsWith(LongPathPrefix, StringComparison.Ordinal) ? LongPathPrefix.Length : 0;
        char[] characters = fileName.ToCharArray();
        for (int index = start; index < characters.Length; index++)
        {
            if (IsBanned(characters[index], allowPath))
            {
                characters[index] = '_';
            }
        }

        return new string(characters);
    }

    private static bool IsBanned(char character, bool allowPath) =>
        char.IsBetween(character, '\u0001', '\u001F')
        || (!allowPath && PathPunctuation.Contains(character, StringComparison.Ordinal))
        || BannedPunctuation.Contains(character, StringComparison.Ordinal);

    private static string ClipTrailingDotsAndSpaces(string fileName) => fileName.TrimEnd(' ', '.');

    /// <summary>
    /// curl's <c>rename_if_reserved_dos</c>: a reserved device name at the start of the name,
    /// and then at the start of its last path component, is renamed (<c>CON</c> to <c>_CON</c>,
    /// <c>CON.EXT</c> to <c>CON_EXT</c>, <c>CON:ADS</c> to <c>CON_ADS</c> without
    /// <c>SANITIZE_ALLOW_PATH</c> and <c>_CON:ADS</c> with it). With paths allowed a name
    /// starting <c>\\</c> is kept whole.
    /// </summary>
    private static string RenameReservedDeviceNames(string fileName, bool allowPath)
    {
        if (allowPath && fileName.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return fileName;
        }

        string renamed = RenameReservedDeviceNameAt(fileName, 0, allowPath);
        int baseStart = BaseNameStart(renamed);
        return baseStart == 0 ? renamed : RenameReservedDeviceNameAt(renamed, baseStart, allowPath);
    }

    private static int BaseNameStart(string path) => path.LastIndexOfAny(['/', '\\']) + 1;

    private static string RenameReservedDeviceNameAt(string name, int start, bool allowPath)
    {
        int length = ReservedDeviceNameLength(name.AsSpan(start));
        if (length == 0)
        {
            return name;
        }

        int next = SkipSpaces(name, start + length);
        if (IsDeviceNameEnd(name, next, allowPath))
        {
            return name.Insert(start, "_");
        }

        // curl also turns a ':' here into '_' when paths are not allowed, but by then
        // ReplaceBannedCharacters has already replaced every ':'.
        return name[next] == '.' ? string.Concat(name.AsSpan(0, next), "_", name.AsSpan(next + 1)) : name;
    }

    private static bool IsDeviceNameEnd(string name, int index, bool allowPath) =>
        index == name.Length || (allowPath && name[index] == ':');

    private static int SkipSpaces(string name, int index)
    {
        while (index < name.Length && name[index] == ' ')
        {
            index++;
        }

        return index;
    }

    private static int ReservedDeviceNameLength(ReadOnlySpan<char> name)
    {
        if (StartsWithAny(name, "CON", "PRN", "AUX", "NUL"))
        {
            return 3;
        }

        if (name.StartsWith("CLOCK$", StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        if (!StartsWithAny(name, "COM", "LPT"))
        {
            return 0;
        }

        return name.Length > 3 && char.IsBetween(name[3], '1', '9') ? 4 : 3;
    }

    private static bool StartsWithAny(ReadOnlySpan<char> name, params ReadOnlySpan<string> prefixes)
    {
        foreach (string prefix in prefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
