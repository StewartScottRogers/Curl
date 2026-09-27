using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// curl 8.21.0's <c>Warning: Failed to open the file &lt;path&gt;: &lt;reason&gt;</c> line, printed
/// when an <c>-o</c> file cannot be created and <c>-s</c> was not given.
/// </summary>
/// <remarks>
/// The reason is the text the Windows C runtime's <c>strerror</c> gives for the failed
/// <c>fopen</c>, each measured with the local curl 8.21.0 on 2026-09-26. curl wraps a
/// warning longer than its terminal width; <see cref="CurlCommandRunner" /> wraps this line
/// with <see cref="WarningLineWrapper" /> as it writes it.
/// </remarks>
internal static class OutputFileOpenWarning
{
    /// <summary>
    /// Builds the warning line, without a line terminator.
    /// </summary>
    /// <param name="path">The file that failed to open: the <c>-o</c> value, after <see cref="WindowsOutputFileNameSanitizer" /> on Windows.</param>
    /// <param name="status">Why the open failed.</param>
    /// <returns>The warning line.</returns>
    internal static string For(string path, FileAccessStatus status) =>
        $"Warning: Failed to open the file {path}: {ReasonFor(status)}";

    /// <summary>
    /// Builds the warning line for a <c>-J</c> name that is already taken, which curl 8.21.0
    /// refuses to overwrite (measured 2026-09-27, BL-239 Notes), without a line terminator.
    /// </summary>
    /// <param name="path">The file that exists.</param>
    /// <returns>The warning line.</returns>
    internal static string ForExistingFile(string path) =>
        $"Warning: Failed to open the file {path}: File exists";

    /// <summary>
    /// The <c>strerror</c> text curl prints for <paramref name="status" />:
    /// <c>No such file or directory</c> for a missing parent directory,
    /// <c>Permission denied</c> for a refusal or a directory, and <c>Invalid argument</c>
    /// for any other failure.
    /// </summary>
    /// <param name="status">Why the open failed.</param>
    /// <returns>The reason text.</returns>
    internal static string ReasonFor(FileAccessStatus status) => status switch
    {
        FileAccessStatus.NotFound => "No such file or directory",
        FileAccessStatus.AccessDenied or FileAccessStatus.IsDirectory => "Permission denied",
        _ => "Invalid argument",
    };
}
