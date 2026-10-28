using System.Diagnostics.CodeAnalysis;
using Curl.Engine;
namespace Curl.Console;

/// <summary>
/// <see cref="IOutputPaths" /> over the real disk.
/// </summary>
/// <param name="isRegularFile">
/// Tells whether a path is a regular file, as curl's <c>stat</c> and <c>S_ISREG</c> do before a
/// <c>--remove-on-error</c> delete; <see langword="null" /> skips the test, as on Windows (ADR-0332).
/// </param>
/// <param name="errorNumbers">The C runtime numbers a failed <c>--create-dirs</c> directory is reported in.</param>
/// <param name="makeDirectoryUnderRawBytes">
/// Makes the directory named by NUL-terminated raw bytes and gives <c>mkdir</c>'s <c>errno</c>, or 0;
/// used for a name holding an argument byte that is not valid UTF-8 (<see cref="RawByteFileName" />,
/// CGR-016, BL-3868). <see langword="null" /> makes every directory through
/// <see cref="Directory.CreateDirectory(string)" />, as on Windows.
/// </param>
/// <param name="rawByteFiles">
/// Stats and deletes a name holding an argument byte that is not valid UTF-8 under its raw bytes,
/// for <see cref="Exists(string)" /> and <see cref="RemoveFile(string)" /> (BL-3947).
/// <see langword="null" /> checks and deletes every name through .NET's file APIs, as on Windows.
/// </param>
internal sealed class PhysicalOutputPaths(
    Func<string, bool>? isRegularFile,
    CRuntimeErrorNumbers errorNumbers,
    Func<byte[], int>? makeDirectoryUnderRawBytes = null,
    RawByteFileName.FileOperations? rawByteFiles = null) : IOutputPaths
{
    /// <summary>
    /// Initializes an instance with the running platform's regular-file test,
    /// <see cref="NativeRegularFileTest.ForCurrentPlatform" />, C runtime numbers and, off Windows,
    /// <see cref="RawByteFileName.MakeDirectoryUnderRawBytes" /> and <see cref="RawByteFileName.FileOperations.Native" />.
    /// </summary>
    public PhysicalOutputPaths()
        : this(
            NativeRegularFileTest.ForCurrentPlatform(),
            CRuntimeErrorNumbers.For(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()),
            OffWindows<Func<byte[], int>>(RawByteFileName.MakeDirectoryUnderRawBytes),
            OffWindows(RawByteFileName.FileOperations.Native))
    {
    }

    /// <summary>
    /// Gives <paramref name="rawByteOperation" /> off Windows and none on Windows, where argument
    /// bytes are never escaped.
    /// </summary>
    /// <typeparam name="T">The operation's type.</typeparam>
    /// <param name="rawByteOperation">A raw-byte file operation.</param>
    /// <returns>The operation, or <see langword="null" />.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083: the platform picks the branch.")]
    private static T? OffWindows<T>(T rawByteOperation)
        where T : class =>
        OperatingSystem.IsWindows() ? null : rawByteOperation;

    /// <inheritdoc />
    public bool TryCreateDirectory(string path, out int errorNumber)
    {
        if (makeDirectoryUnderRawBytes is { } makeRaw && RawByteFileName.HoldsEscapedByte(path))
        {
            return TryMakeDirectoryUnderRawBytes(makeRaw, path, out errorNumber);
        }

        errorNumber = 0;
        if (Exists(path))
        {
            return true;
        }

        if (ParentIsRegularFile(path))
        {
            errorNumber = CRuntimeErrorNumbers.NotADirectory;

            return false;
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

    /// <summary>
    /// Makes the directory <paramref name="path" /> under its raw bytes with
    /// <paramref name="makeRaw" />; one that already exists counts as made, as for every other name.
    /// </summary>
    /// <param name="makeRaw">Makes the directory and gives <c>mkdir</c>'s <c>errno</c>, or 0.</param>
    /// <param name="path">The directory's name, holding an escaped byte.</param>
    /// <param name="errorNumber">The <c>errno</c> when the directory could not be made, otherwise 0.</param>
    /// <returns><see langword="true" /> when the directory is there afterwards.</returns>
    internal static bool TryMakeDirectoryUnderRawBytes(Func<byte[], int> makeRaw, string path, out int errorNumber)
    {
        errorNumber = makeRaw(RawByteFileName.NullTerminatedBytes(path));
        if (errorNumber == CRuntimeErrorNumbers.AlreadyExists)
        {
            errorNumber = 0;
        }

        return errorNumber == 0;
    }

    /// <inheritdoc />
    public bool Exists(string path) => rawByteFiles is { } raw && RawByteFileName.HoldsEscapedByte(path)
        ? raw.FileTypeOf(RawByteFileName.NullTerminatedBytes(path)) >= 0
        : File.Exists(path) || Directory.Exists(path);

    /// <summary>
    /// Tells whether the directory <paramref name="path" /> would be created in is a regular file,
    /// where <c>mkdir</c> fails with <c>ENOTDIR</c> (<c>ENOENT</c> on Windows; both give curl's
    /// <c>Error creating directory</c>). .NET on Windows reports that file as
    /// <c>ERROR_ALREADY_EXISTS</c> instead, an <c>EEXIST</c> curl would ignore (CGR-048, BL-3577).
    /// </summary>
    /// <param name="path">The directory to create.</param>
    /// <returns>
    /// <see langword="true" /> when <paramref name="path" />'s parent is a file; a path with no
    /// parent gives <see langword="null" /> or an empty string, which <see cref="File.Exists(string)" /> rejects.
    /// </returns>
    private static bool ParentIsRegularFile(string path) => File.Exists(Path.GetDirectoryName(path));

    /// <inheritdoc />
    public OutputFileRemoval RemoveFile(string path)
    {
        if (rawByteFiles is { } raw && RawByteFileName.HoldsEscapedByte(path))
        {
            return RemoveFileUnderRawBytes(raw, path);
        }

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
    /// Deletes <paramref name="path" /> under its raw bytes when <c>stat</c> finds a regular file
    /// there, as curl's <c>--remove-on-error</c> does on Linux and macOS.
    /// </summary>
    /// <param name="raw">The raw-byte <c>stat</c> and <c>unlink</c>.</param>
    /// <param name="path">The output file's name, holding an escaped byte.</param>
    /// <returns>How the removal went.</returns>
    private static OutputFileRemoval RemoveFileUnderRawBytes(RawByteFileName.FileOperations raw, string path)
    {
        byte[] name = RawByteFileName.NullTerminatedBytes(path);
        if (raw.FileTypeOf(name) != RawByteFileName.RegularFileType)
        {
            return OutputFileRemoval.NotRegularFile;
        }

        return raw.Remove(name) ? OutputFileRemoval.Removed : OutputFileRemoval.Failed;
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
                // ERROR_DISK_FULL (112) is ENOSPC; ERROR_FILE_EXISTS (80) and ERROR_ALREADY_EXISTS (183) are EEXIST.
                112 => CRuntimeErrorNumbers.NoSpaceLeft,
                80 or 183 => CRuntimeErrorNumbers.AlreadyExists,
                _ => InvalidArgument,
            };
    }
}
