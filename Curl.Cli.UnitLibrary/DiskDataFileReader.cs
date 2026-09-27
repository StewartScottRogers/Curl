namespace Curl.Cli;

/// <summary>
/// The <see cref="IDataFileReader"/> backed by the disk and this process's standard input. A file
/// that cannot be opened or read (missing, a directory, access denied, an empty or malformed path),
/// or whose modification time cannot be looked up, is reported as unread rather than thrown.
/// </summary>
/// <param name="readAllBytes">Reads every byte of a file, throwing as <see cref="File.ReadAllBytes(string)"/> does.</param>
/// <param name="openStandardInput">Opens standard input for reading.</param>
/// <param name="readLastWriteTimeUtc">Reads a file's last write time in UTC, throwing as <see cref="File.GetAttributes(string)"/> does when the file cannot be found, or a <see cref="FileTimeLookupException"/> naming the Windows call that failed.</param>
/// <param name="reportsWindowsErrors">
/// Whether a failed modification-time lookup is reported as curl's Windows build reports it
/// (<c>CreateFile failed: GetLastError 0x0000000N</c>, nothing for file not found); when
/// <see langword="false"/> it is reported as curl's Linux and macOS builds report a failed
/// <c>stat</c>, by its <c>strerror</c> text, file not found included.
/// </param>
public sealed class DiskDataFileReader(Func<string, byte[]> readAllBytes, Func<Stream> openStandardInput, Func<string, DateTime> readLastWriteTimeUtc, bool reportsWindowsErrors) : IDataFileReader
{
    private const int ErrorFileNotFound = 2;

    private const int ErrorPathNotFound = 3;

    private const int Win32HResultFacility = unchecked((int)0x80070000);

    private const string NoSuchFileOrDirectory = "No such file or directory";

    private const string NotADirectory = "Not a directory";

    private const string TooManyLevelsOfSymbolicLinks = "Too many levels of symbolic links";

    private static readonly Func<string, byte[]> ReadAllBytesFromDisk = File.ReadAllBytes;

    private static readonly Func<Stream> OpenProcessStandardInput = Console.OpenStandardInput;

    /// <summary>
    /// The reader over the disk for the operating system this process runs on:
    /// <see cref="ForPlatform"/> <see cref="OperatingSystem.IsWindows()"/>.
    /// </summary>
    public static DiskDataFileReader ForProcess { get; } = ForPlatform(OperatingSystem.IsWindows());

    /// <summary>
    /// The reader over the disk, through <see cref="File.ReadAllBytes(string)"/> and
    /// <see cref="Console.OpenStandardInput()"/>, that looks up a modification time as curl's build
    /// for the given operating system does: on Windows by opening the file, as <c>CreateFile</c>
    /// does, so a directory fails; elsewhere as <c>stat</c> does, so a directory or a file this
    /// process may not read has a modification time, and a path through a file fails as
    /// <c>Not a directory</c>.
    /// </summary>
    /// <param name="isWindows">Whether to behave as curl's Windows build.</param>
    /// <returns>The reader.</returns>
    public static DiskDataFileReader ForPlatform(bool isWindows) =>
        isWindows
            ? new(ReadAllBytesFromDisk, OpenProcessStandardInput, WindowsFileTimeReader.ReadLastWriteTimeUtc, true)
            : ForStatFollowingLinksWith(ResolveFinalLinkTarget);

    /// <summary>
    /// The reader over the disk that looks up a modification time as curl's Linux and macOS builds
    /// do with <c>stat</c>, following a symbolic link to its final target through
    /// <paramref name="resolveFinalLinkTarget"/>: a link whose final target is missing fails as
    /// <c>No such file or directory</c>, a link loop as <c>Too many levels of symbolic links</c>, and
    /// a link to an existing file has that file's time (ADR-0090).
    /// </summary>
    /// <param name="resolveFinalLinkTarget">
    /// Resolves a path to the final target of its symbolic link, as
    /// <see cref="File.ResolveLinkTarget(string, bool)"/> does with <c>returnFinalTarget</c>:
    /// <see langword="null"/> when the path is not a link, and an <see cref="IOException"/> of exactly
    /// that type when the links loop.
    /// </param>
    /// <returns>The reader.</returns>
    public static DiskDataFileReader ForStatFollowingLinksWith(Func<string, FileSystemInfo?> resolveFinalLinkTarget) =>
        new(ReadAllBytesFromDisk, OpenProcessStandardInput, path => ReadLastWriteTimeUtcByStat(path, resolveFinalLinkTarget), false);

    /// <inheritdoc/>
    public bool TryReadFile(string path, out byte[] contents)
    {
        try
        {
            contents = readAllBytes(path);
            return true;
        }
        catch (Exception exception) when (IsUnreadableFile(exception))
        {
            contents = [];
            return false;
        }
    }

    /// <inheritdoc/>
    public byte[] ReadStandardInput()
    {
        using Stream standardInput = openStandardInput();
        using MemoryStream contents = new();
        standardInput.CopyTo(contents);
        return contents.ToArray();
    }

    /// <inheritdoc/>
    public bool TryReadModificationTime(string path, out DateTimeOffset modificationTime, out string? failureReason)
    {
        try
        {
            long seconds = new DateTimeOffset(readLastWriteTimeUtc(path), TimeSpan.Zero).ToUnixTimeSeconds();
            modificationTime = DateTimeOffset.FromUnixTimeSeconds(seconds);
            failureReason = null;
            return true;
        }
        catch (Exception exception) when (IsUnreadableFile(exception))
        {
            modificationTime = default;
            failureReason = DescribeLookupFailure(exception);
            return false;
        }
    }

    /// <summary>
    /// curl's Windows build opens the file with <c>CreateFile</c>, reads its time with
    /// <c>GetFileTime</c> and, for any error but <c>ERROR_FILE_NOT_FOUND</c>, names the call that
    /// failed (<see cref="FileTimeLookupException.FailedCall"/>, else <c>CreateFile</c>) and the error
    /// code, which the exception carries in its <see cref="Exception.HResult"/>. An empty or malformed path, which .NET refuses before asking
    /// Windows, and a failure carrying no Windows error code read as <c>ERROR_PATH_NOT_FOUND</c>, as
    /// curl 8.21.0 reports <c>-z ""</c>.
    /// </summary>
    private string? DescribeLookupFailure(Exception exception) =>
        reportsWindowsErrors ? DescribeCreateFileFailure(exception) : DescribeStatFailure(exception);

    private static string? DescribeCreateFileFailure(Exception exception)
    {
        int errorCode = exception is not ArgumentException && (exception.HResult & unchecked((int)0xFFFF0000)) == Win32HResultFacility
            ? exception.HResult & 0xFFFF
            : ErrorPathNotFound;
        string failedCall = exception is FileTimeLookupException lookupFailure ? lookupFailure.FailedCall : "CreateFile";
        return errorCode != ErrorFileNotFound
            ? $"{failedCall} failed: GetLastError 0x{errorCode:x8}"
            : null;
    }

    /// <summary>
    /// curl's Linux and macOS builds print <c>strerror(errno)</c> for a failed <c>stat</c>. .NET maps
    /// <c>ENOENT</c>, <c>EACCES</c> and <c>ENAMETOOLONG</c> to their own exception types, and words any
    /// other <c>errno</c> as <c>&lt;strerror&gt; : '&lt;path&gt;'</c>. An empty path, which .NET refuses
    /// before asking the system, reads as <c>ENOENT</c>, as curl 8.21.0 reports <c>-z ""</c>.
    /// </summary>
    private static string DescribeStatFailure(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Permission denied",
        PathTooLongException => "File name too long",
        FileNotFoundException or DirectoryNotFoundException => NoSuchFileOrDirectory,
        IOException => StrErrorTextOf(exception.Message),
        _ => NoSuchFileOrDirectory,
    };

    private static string StrErrorTextOf(string message)
    {
        int pathStart = message.IndexOf(" : '", StringComparison.Ordinal);
        return pathStart < 0 ? message : message[..pathStart];
    }


    /// <summary>
    /// Stands in for <c>stat</c>: <see cref="File.GetAttributes(string)"/> fails as it does, except that
    /// .NET reports <c>ENOTDIR</c> as a missing directory and ignores a trailing separator after a
    /// file, so both are told apart here by looking for the file in the way. <see cref="File.GetAttributes(string)"/>
    /// does not follow a final symbolic link, so the link is followed afterwards.
    /// </summary>
    private static DateTime ReadLastWriteTimeUtcByStat(string path, Func<string, FileSystemInfo?> resolveFinalLinkTarget)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (DirectoryNotFoundException) when (HasFileAncestor(path))
        {
            throw new IOException(NotADirectory);
        }

        if (Path.EndsInDirectorySeparator(path) && !attributes.HasFlag(FileAttributes.Directory))
        {
            throw new IOException(NotADirectory);
        }

        return File.GetLastWriteTimeUtc(ResolveFinalLinkTargetAsStatDoes(path, resolveFinalLinkTarget)?.FullName ?? path);
    }

    /// <summary>
    /// A missing final target fails as <c>stat</c> does: <c>ENOTDIR</c> when a file stands in its
    /// path, otherwise <c>ENOENT</c>; a link loop fails as <c>ELOOP</c>. Existence is asked of the
    /// path, not the <see cref="FileSystemInfo"/>, because <see cref="File.ResolveLinkTarget(string, bool)"/>
    /// returns a <see cref="FileInfo"/> even for a directory.
    /// </summary>
    private static FileSystemInfo? ResolveFinalLinkTargetAsStatDoes(string path, Func<string, FileSystemInfo?> resolveFinalLinkTarget)
    {
        FileSystemInfo? linkTarget;
        try
        {
            linkTarget = resolveFinalLinkTarget(path);
        }
        catch (IOException exception) when (exception.GetType() == typeof(IOException))
        {
            throw new IOException(TooManyLevelsOfSymbolicLinks, exception);
        }

        if (linkTarget is not null && !Path.Exists(linkTarget.FullName))
        {
            throw HasFileAncestor(linkTarget.FullName) ? new IOException(NotADirectory) : new FileNotFoundException(NoSuchFileOrDirectory);
        }

        return linkTarget;
    }

    private static FileSystemInfo? ResolveFinalLinkTarget(string path) =>
        File.ResolveLinkTarget(Path.GetFullPath(path), returnFinalTarget: true);

    private static bool HasFileAncestor(string path)
    {
        for (string? ancestor = Path.GetDirectoryName(path); !string.IsNullOrEmpty(ancestor); ancestor = Path.GetDirectoryName(ancestor))
        {
            if (File.Exists(ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnreadableFile(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
