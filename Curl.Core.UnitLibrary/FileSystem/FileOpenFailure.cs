using Curl.Protocol.Abstractions;

namespace Curl.Core.FileSystem;

/// <summary>
/// Maps the exception a <see cref="System.IO" /> open throws onto the
/// <see cref="FileAccessStatus" /> an <see cref="IFileSystem" /> must return instead.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="PhysicalFileSystem" /> so every mapping is tested without a
/// disk: the exception types are the input, and whether the path names a directory is
/// passed in rather than looked up.
/// </remarks>
internal static class FileOpenFailure
{
    /// <summary>
    /// Tells whether <paramref name="exception" /> is one an open can raise for a path the
    /// operating system rejects, which <see cref="PhysicalFileSystem" /> must absorb.
    /// </summary>
    /// <param name="exception">The exception the open threw.</param>
    /// <returns>
    /// <see langword="true" /> for <see cref="ArgumentException" />,
    /// <see cref="NotSupportedException" />, <see cref="UnauthorizedAccessException" /> and
    /// <see cref="IOException" /> with its subclasses - <see cref="PathTooLongException" />,
    /// <see cref="DirectoryNotFoundException" /> and <see cref="FileNotFoundException" /> -
    /// which is the list ADR-0002 obliges an implementation to absorb. Anything else,
    /// cancellation included, is left to propagate.
    /// </returns>
    internal static bool IsOpenFailure(Exception exception) =>
        exception is ArgumentException
            or NotSupportedException
            or UnauthorizedAccessException
            or IOException;

    /// <summary>
    /// Chooses the <see cref="FileAccessStatus" /> for a failed open.
    /// </summary>
    /// <param name="exception">The exception the open threw.</param>
    /// <param name="pathIsDirectory">
    /// Whether the path names an existing directory. Neither Windows nor Linux reports
    /// that through a distinct exception type - both throw
    /// <see cref="UnauthorizedAccessException" /> - so it is asked of the file system
    /// separately and wins over the exception type.
    /// </param>
    /// <returns>
    /// <see cref="FileAccessStatus.IsDirectory" /> for a directory;
    /// <see cref="FileAccessStatus.NotFound" /> for a missing file or parent directory;
    /// <see cref="FileAccessStatus.AccessDenied" /> for a refusal; and
    /// <see cref="FileAccessStatus.IoError" /> for everything else, an invalid or
    /// over-long path included.
    /// </returns>
    internal static FileAccessStatus StatusFor(Exception exception, bool pathIsDirectory)
    {
        if (pathIsDirectory)
        {
            return FileAccessStatus.IsDirectory;
        }

        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => FileAccessStatus.NotFound,
            UnauthorizedAccessException => FileAccessStatus.AccessDenied,
            _ => FileAccessStatus.IoError,
        };
    }
}
