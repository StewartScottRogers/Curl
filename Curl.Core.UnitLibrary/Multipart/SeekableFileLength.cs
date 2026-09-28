namespace Curl.Core.Multipart;

/// <summary>
/// The length curl 8.21.0's <c>stat</c> declares for a <c>-F</c> file that can seek, which
/// decides whether the multipart body has a <c>Content-Length</c> or goes chunked.
/// </summary>
/// <remarks>
/// <para>
/// libcurl's <c>curl_mime_filedata</c> gives a file part the size <c>stat</c> reports only when
/// <c>stat</c> calls it a regular file, and an unknown size, so a chunked body, otherwise. On
/// Linux and macOS a character device such as <c>/dev/null</c> or <c>/dev/zero</c> accepts
/// <c>lseek</c>, so it opens seekable, yet it is not a regular file: curl sends it chunked
/// (measured in BL-445). On Windows every file that seeks is a regular file to the C runtime's
/// <c>_wstati64</c>, so its length stands.
/// </para>
/// <para>
/// The base class library does not report a file's type, so off Windows a device is known by its
/// path: one under <c>/dev/</c>, except <c>/dev/shm/</c>, whose files are regular, and
/// <c>/dev/stdin</c>, <c>/dev/stdout</c>, <c>/dev/stderr</c> and <c>/dev/fd/</c>, which name
/// whatever the descriptor was opened on - a regular file when it seeks, as a redirect gives it.
/// The path is taken as given, so a relative path into <c>/dev</c> keeps its length. See ADR-0104.
/// </para>
/// </remarks>
public static class SeekableFileLength
{
    private const string DeviceDirectory = "/dev/";

    private static readonly string[] RegularFilePrefixes = ["/dev/shm/", "/dev/fd/"];

    private static readonly string[] DescriptorPaths = ["/dev/stdin", "/dev/stdout", "/dev/stderr"];

    /// <summary>The platform curl's rule: <see cref="AsWindowsStatReportsIt" /> on Windows, <see cref="AsPosixStatReportsIt" /> elsewhere.</summary>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <returns>The length each seekable file declares from its path and opened length, or <see langword="null" /> for none.</returns>
    public static Func<string, long, long?> ForPlatform(bool runsOnWindows) =>
        runsOnWindows ? AsWindowsStatReportsIt : AsPosixStatReportsIt;

    /// <summary>Declares the opened length, as the Windows C runtime's <c>stat</c> calls every seekable file regular.</summary>
    /// <param name="path">The file's path, which does not matter.</param>
    /// <param name="length">The opened file's length.</param>
    /// <returns><paramref name="length" />.</returns>
    public static long? AsWindowsStatReportsIt(string path, long length) => length;

    /// <summary>
    /// Declares no length for a device, so the body goes chunked, and the opened length for any
    /// other file, as curl on Linux and macOS sends them.
    /// </summary>
    /// <param name="path">The path the part names, such as <c>/dev/null</c>.</param>
    /// <param name="length">The opened file's length.</param>
    /// <returns><see langword="null" /> for a device; otherwise <paramref name="length" />.</returns>
    public static long? AsPosixStatReportsIt(string path, long length) =>
        IsDevicePath(path) ? null : length;

    /// <summary>Whether <paramref name="path" /> names a device under <c>/dev/</c> rather than a regular file there.</summary>
    /// <param name="path">The path the part names.</param>
    /// <returns><see langword="true" /> for a device path.</returns>
    internal static bool IsDevicePath(string path) =>
        path.StartsWith(DeviceDirectory, StringComparison.Ordinal)
        && !RegularFilePrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal))
        && !DescriptorPaths.Contains(path, StringComparer.Ordinal);
}
