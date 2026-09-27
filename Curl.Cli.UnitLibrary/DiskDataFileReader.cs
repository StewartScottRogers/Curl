using Microsoft.Win32.SafeHandles;

namespace Curl.Cli;

/// <summary>
/// The <see cref="IDataFileReader"/> backed by the disk and this process's standard input. A file
/// that cannot be opened or read (missing, a directory, access denied, an empty or malformed path)
/// is reported as unread rather than thrown.
/// </summary>
/// <param name="readAllBytes">Reads every byte of a file, throwing as <see cref="File.ReadAllBytes(string)"/> does.</param>
/// <param name="openStandardInput">Opens standard input for reading.</param>
/// <param name="readLastWriteTimeUtc">Reads a file's last write time in UTC, throwing as <see cref="File.OpenHandle"/> does when the file cannot be opened.</param>
/// <param name="reportsWindowsErrors">
/// Whether a failed modification-time lookup is reported as curl's Windows build reports it
/// (<c>CreateFile failed: GetLastError 0x0000000N</c>); when <see langword="false"/> every failure
/// reads as file not found, which prints no extra line.
/// </param>
public sealed class DiskDataFileReader(Func<string, byte[]> readAllBytes, Func<Stream> openStandardInput, Func<string, DateTime> readLastWriteTimeUtc, bool reportsWindowsErrors) : IDataFileReader
{
    private const int ErrorFileNotFound = 2;

    private const int ErrorPathNotFound = 3;

    private const int Win32HResultFacility = unchecked((int)0x80070000);

    /// <summary>
    /// The reader over the disk, through <see cref="File.ReadAllBytes(string)"/>,
    /// <see cref="Console.OpenStandardInput()"/> and <see cref="File.GetLastWriteTimeUtc(SafeFileHandle)"/>,
    /// reporting lookup failures as curl's Windows build does when running on Windows.
    /// </summary>
    public static DiskDataFileReader ForProcess { get; } =
        new(File.ReadAllBytes, Console.OpenStandardInput, ReadLastWriteTimeUtcFromDisk, OperatingSystem.IsWindows());

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
    /// curl's Windows build opens the file with <c>CreateFile</c> and, for any error but
    /// <c>ERROR_FILE_NOT_FOUND</c>, names the error code, which .NET carries in the exception's
    /// <see cref="Exception.HResult"/>. An empty or malformed path, which .NET refuses before asking
    /// Windows, and a failure carrying no Windows error code read as <c>ERROR_PATH_NOT_FOUND</c>, as
    /// curl 8.21.0 reports <c>-z ""</c>.
    /// </summary>
    private string? DescribeLookupFailure(Exception exception)
    {
        int errorCode = exception is not ArgumentException && (exception.HResult & unchecked((int)0xFFFF0000)) == Win32HResultFacility
            ? exception.HResult & 0xFFFF
            : ErrorPathNotFound;
        return reportsWindowsErrors && errorCode != ErrorFileNotFound
            ? $"CreateFile failed: GetLastError 0x{errorCode:x8}"
            : null;
    }

    private static DateTime ReadLastWriteTimeUtcFromDisk(string path)
    {
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return File.GetLastWriteTimeUtc(handle);
    }

    private static bool IsUnreadableFile(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
