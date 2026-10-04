namespace Curl.Console;

/// <summary>
/// <see cref="IOutputPaths" /> over the real disk.
/// </summary>
/// <param name="isRegularFile">
/// Tells whether a path is a regular file, as curl's <c>stat</c> and <c>S_ISREG</c> do before a
/// <c>--remove-on-error</c> delete; <see langword="null" /> skips the test, as on Windows (ADR-0332).
/// </param>
/// <param name="errorNumbers">The C runtime numbers a failed <c>--create-dirs</c> directory is reported in.</param>
internal sealed class PhysicalOutputPaths(Func<string, bool>? isRegularFile, CRuntimeErrorNumbers errorNumbers) : IOutputPaths
{
    /// <summary>
    /// Initializes an instance with the running platform's regular-file test,
    /// <see cref="NativeRegularFileTest.ForCurrentPlatform" />, and C runtime numbers.
    /// </summary>
    public PhysicalOutputPaths()
        : this(NativeRegularFileTest.ForCurrentPlatform(), CRuntimeErrorNumbers.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()))
    {
    }

    /// <inheritdoc />
    public bool TryCreateDirectory(string path, out int errorNumber)
    {
        errorNumber = 0;
        if (Exists(path))
        {
            return true;
        }

        try
        {
            Directory.CreateDirectory(path);

            return true;
        }
        catch (Exception exception) when (IsCreateFailure(exception))
        {
            errorNumber = ErrorNumberOf(exception, errorNumbers);

            return false;
        }
    }

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <inheritdoc />
    public OutputFileRemoval RemoveFile(string path)
    {
        if (isRegularFile is not null && !isRegularFile(path))
        {
            return OutputFileRemoval.NotRegularFile;
        }

        if (!File.Exists(path))
        {
            return OutputFileRemoval.Failed;
        }

        try
        {
            File.Delete(path);

            return OutputFileRemoval.Removed;
        }
        catch (Exception exception) when (IsDeleteFailure(exception))
        {
            return OutputFileRemoval.Failed;
        }
    }

    /// <summary>
    /// Tells whether <paramref name="exception" /> is one <see cref="File.Delete(string)" /> raises
    /// for a file the operating system will not delete, such as one another process holds open.
    /// </summary>
    /// <param name="exception">The exception the delete threw.</param>
    /// <returns>
    /// <see langword="true" /> for an <see cref="IOException" /> or an
    /// <see cref="UnauthorizedAccessException" />.
    /// </returns>
    internal static bool IsDeleteFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    /// <summary>
    /// Tells whether <paramref name="exception" /> is one <see cref="Directory.CreateDirectory(string)" />
    /// raises for a directory the operating system will not create.
    /// </summary>
    /// <param name="exception">The exception the create threw.</param>
    /// <returns>
    /// <see langword="true" /> for an <see cref="IOException" />, an
    /// <see cref="UnauthorizedAccessException" />, an <see cref="ArgumentException" /> or a
    /// <see cref="NotSupportedException" />.
    /// </returns>
    internal static bool IsCreateFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;

    /// <summary>
    /// Gives the C runtime's <c>errno</c> for a failed <see cref="Directory.CreateDirectory(string)" />,
    /// as curl's <c>mkdir</c> would have seen it.
    /// </summary>
    /// <param name="exception">The exception the create threw.</param>
    /// <param name="errorNumbers">The platform's C runtime numbers.</param>
    /// <returns>
    /// <c>EACCES</c> for an <see cref="UnauthorizedAccessException" /> (.NET folds <c>EPERM</c> into
    /// it), <c>ENAMETOOLONG</c> for a <see cref="PathTooLongException" />, <c>ENOENT</c> for a
    /// missing parent, the C runtime's mapping of a Win32 error code, and otherwise the
    /// exception's <see cref="Exception.HResult" />, which .NET sets to the raw <c>errno</c> of an
    /// <see cref="IOException" /> it has no type for off Windows (<c>EROFS</c>, <c>ENOSPC</c>, <c>EDQUOT</c>).
    /// </returns>
    internal static int ErrorNumberOf(Exception exception, CRuntimeErrorNumbers errorNumbers) => exception switch
    {
        UnauthorizedAccessException => CRuntimeErrorNumbers.PermissionDenied,
        PathTooLongException => errorNumbers.NameTooLong,
        DirectoryNotFoundException or FileNotFoundException => CRuntimeErrorNumbers.NoSuchFileOrDirectory,
        _ => ErrorNumberOfHResult(exception.HResult),
    };

    /// <summary>
    /// Maps an <see cref="Exception.HResult" /> to an <c>errno</c>: a Win32 error code (facility
    /// 7) as the Windows C runtime's <c>_dosmaperr</c> maps it for the codes curl's messages tell
    /// apart, <c>EINVAL</c> for the rest; anything else is taken as the raw <c>errno</c> itself.
    /// </summary>
    /// <param name="hresult">The exception's <see cref="Exception.HResult" />.</param>
    /// <returns>The errno.</returns>
    private static int ErrorNumberOfHResult(int hresult)
    {
        const int FacilityWin32Mask = unchecked((int)0xFFFF0000);
        const int FacilityWin32 = unchecked((int)0x80070000);
        const int InvalidArgument = 22;

        return (hresult & FacilityWin32Mask) != FacilityWin32
            ? hresult
            : (hresult & 0xFFFF) switch
            {
                112 => CRuntimeErrorNumbers.NoSpaceLeft,
                80 or 183 => CRuntimeErrorNumbers.AlreadyExists,
                _ => InvalidArgument,
            };
    }
}
