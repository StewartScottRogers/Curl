namespace Curl.Console;

/// <summary>
/// The C runtime's <c>errno</c> values curl 8.21.0's <c>--create-dirs</c> and <c>-R</c> messages
/// depend on, for one platform, and its <c>strerror</c> text for them (BL-1433).
/// </summary>
/// <remarks>
/// Linux (glibc), macOS and the Windows C runtime agree on <c>EPERM</c> 1, <c>ENOENT</c> 2,
/// <c>EIO</c> 5, <c>EACCES</c> 13, <c>EEXIST</c> 17, <c>ENOTDIR</c> 20, <c>ENOSPC</c> 28 and
/// <c>EROFS</c> 30, and differ on <c>ENAMETOOLONG</c> (36, 63, 38), <c>ELOOP</c> (40, 62, 114)
/// and <c>EDQUOT</c> (122, 69, none on Windows). glibc words an errno it has no text for
/// <c>Unknown error &lt;n&gt;</c>, macOS <c>Unknown error: &lt;n&gt;</c>.
/// </remarks>
/// <param name="NameTooLong">The platform's <c>ENAMETOOLONG</c>.</param>
/// <param name="TooManySymbolicLinks">The platform's <c>ELOOP</c>.</param>
/// <param name="QuotaExceeded">The platform's <c>EDQUOT</c>, or <see langword="null" /> when it has none.</param>
/// <param name="UnknownErrorPrefix">What <c>strerror</c> puts before an errno it has no text for.</param>
internal sealed record CRuntimeErrorNumbers(int NameTooLong, int TooManySymbolicLinks, int? QuotaExceeded, string UnknownErrorPrefix)
{
    /// <summary><c>EPERM</c>.</summary>
    internal const int OperationNotPermitted = 1;

    /// <summary><c>ENOENT</c>.</summary>
    internal const int NoSuchFileOrDirectory = 2;

    /// <summary><c>EIO</c>.</summary>
    internal const int InputOutputError = 5;

    /// <summary><c>EACCES</c>.</summary>
    internal const int PermissionDenied = 13;

    /// <summary><c>EEXIST</c>.</summary>
    internal const int AlreadyExists = 17;

    /// <summary><c>ENOTDIR</c>.</summary>
    internal const int NotADirectory = 20;

    /// <summary><c>ENOSPC</c>.</summary>
    internal const int NoSpaceLeft = 28;

    /// <summary><c>EROFS</c>.</summary>
    internal const int ReadOnlyFileSystem = 30;

    /// <summary>The <c>strerror</c> text of every errno whose number all three platforms share.</summary>
    private static readonly Dictionary<int, string> SharedDescriptions = new()
    {
        [OperationNotPermitted] = "Operation not permitted",
        [NoSuchFileOrDirectory] = "No such file or directory",
        [InputOutputError] = "Input/output error",
        [PermissionDenied] = "Permission denied",
        [AlreadyExists] = "File exists",
        [NotADirectory] = "Not a directory",
        [NoSpaceLeft] = "No space left on device",
        [ReadOnlyFileSystem] = "Read-only file system",
    };

    /// <summary>Linux's, with glibc's <c>strerror</c>.</summary>
    internal static readonly CRuntimeErrorNumbers Linux = new(36, 40, 122, "Unknown error ");

    /// <summary>macOS's.</summary>
    internal static readonly CRuntimeErrorNumbers MacOS = new(63, 62, 69, "Unknown error: ");

    /// <summary>The Windows C runtime's, which <c>_wmkdir</c> maps Win32 errors to.</summary>
    internal static readonly CRuntimeErrorNumbers Windows = new(38, 114, null, "Unknown error ");

    /// <summary>
    /// Picks the table for a platform.
    /// </summary>
    /// <param name="runsOnWindows">Whether curl's Windows build is the one matched.</param>
    /// <param name="runsOnMacOS">Whether the process runs on macOS.</param>
    /// <returns><see cref="Windows" />, <see cref="MacOS" /> or <see cref="Linux" />.</returns>
    internal static CRuntimeErrorNumbers For(bool runsOnWindows, bool runsOnMacOS) =>
        runsOnWindows ? Windows : runsOnMacOS ? MacOS : Linux;

    /// <summary>
    /// Gives the C library's <c>strerror</c> text for <paramref name="errorNumber" />.
    /// </summary>
    /// <param name="errorNumber">The errno.</param>
    /// <returns>The text, without a line terminator.</returns>
    internal string Describe(int errorNumber) =>
        SharedDescriptions.TryGetValue(errorNumber, out string? shared) ? shared
        : errorNumber == NameTooLong ? "File name too long"
        : errorNumber == TooManySymbolicLinks ? "Too many levels of symbolic links"
        : UnknownErrorPrefix + errorNumber;
}
