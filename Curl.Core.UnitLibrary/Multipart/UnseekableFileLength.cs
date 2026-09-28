using System.IO.Enumeration;

namespace Curl.Core.Multipart;

/// <summary>
/// The length curl 8.21.0's <c>stat</c> declares for a <c>-F</c> file that cannot seek - a pipe
/// or a device - which decides whether the multipart body has a <c>Content-Length</c> or goes
/// chunked.
/// </summary>
/// <remarks>
/// <para>
/// libcurl's <c>curl_mime_filedata</c> gives a file part the size <c>stat</c> reports when
/// <c>stat</c> calls it a regular file, and an unknown size, so a chunked body, otherwise. On
/// Linux and macOS a FIFO or a character device is not a regular file, so the body goes
/// chunked. On Windows the C runtime's <c>_wstati64</c> calls a named pipe and <c>NUL</c> regular
/// files: <c>NUL</c> has the size 0, and a named pipe the size its directory entry in
/// <c>\\.\pipe\</c> reports, which is the number of instances its server has created. The
/// body then declares that length and is cut off at it, whatever the pipe delivers. See ADR-0097.
/// </para>
/// </remarks>
public static class UnseekableFileLength
{
    private const string PipeDirectory = @"\\.\pipe\";

    /// <summary>The platform curl's rule: <see cref="AsWindowsStatReportsIt" /> on Windows, <see cref="Unknown" /> elsewhere.</summary>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <returns>The length each unseekable file's path declares, or <see langword="null" /> for none.</returns>
    public static Func<string, long?> ForPlatform(bool runsOnWindows) =>
        runsOnWindows ? AsWindowsStatReportsIt : Unknown;

    /// <summary>Declares no length, so the body goes chunked, as curl sends a FIFO or a device on Linux and macOS.</summary>
    /// <param name="path">The file's path, which does not matter.</param>
    /// <returns><see langword="null" />.</returns>
    public static long? Unknown(string path) => null;

    /// <summary>
    /// The size the Windows C runtime's <c>stat</c> reports for <paramref name="path" />: a named
    /// pipe's instance count, and zero for any other file that cannot seek, such as <c>NUL</c>.
    /// </summary>
    /// <param name="path">The path the part names, such as <c>\\.\pipe\name</c> or <c>NUL</c>.</param>
    /// <returns>The size in bytes curl declares for it.</returns>
    public static long? AsWindowsStatReportsIt(string path) =>
        TryGetLocalPipeName(path, out string? name) ? PipeInstanceCount(name) : 0;

    /// <summary>Finds the pipe name in a <c>\\.\pipe\</c> or <c>\\?\pipe\</c> path, with either slash.</summary>
    /// <param name="path">The path.</param>
    /// <param name="name">The pipe's name, when the path names a local pipe.</param>
    /// <returns><see langword="true" /> when <paramref name="path" /> names a local pipe.</returns>
    internal static bool TryGetLocalPipeName(string path, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? name)
    {
        string normal = path.Replace('/', '\\');
        bool isPipe = normal.Length > PipeDirectory.Length
            && (normal.StartsWith(PipeDirectory, StringComparison.OrdinalIgnoreCase)
                || normal.StartsWith(@"\\?\pipe\", StringComparison.OrdinalIgnoreCase));
        name = isPipe ? normal[PipeDirectory.Length..] : null;
        return isPipe;
    }

    /// <summary>The instance count <c>\\.\pipe\</c> lists for the pipe <paramref name="name" />, or zero when it lists no such pipe.</summary>
    /// <param name="name">The pipe's name.</param>
    /// <returns>The number of instances its server has created.</returns>
    internal static long PipeInstanceCount(string name)
    {
        FileSystemEnumerable<long> instanceCounts = new(
            PipeDirectory,
            (ref FileSystemEntry entry) => entry.Length,
            new EnumerationOptions { IgnoreInaccessible = true })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
                entry.FileName.Equals(name, StringComparison.OrdinalIgnoreCase),
        };

        return instanceCounts.FirstOrDefault();
    }
}
