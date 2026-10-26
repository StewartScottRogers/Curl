using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Curl.Cli;
using Curl.Protocol.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace Curl.Console;

/// <summary>
/// Opens an output file whose name holds argument bytes that are not valid UTF-8 under those raw
/// bytes, as curl 8.18.0 does on Linux and macOS (CGR-016, BL-3853, ADR-0822): <c>-o $'\xff.txt'</c>
/// creates the file named FF 2E 74 78 74. Such a byte reaches the name as the lone surrogate
/// U+DC00 + byte (<see cref="InvalidUtf8ByteEscape" />), which .NET's file APIs would write as
/// EF BF BD, so the name is opened with the runtime's own <c>open</c> shim instead. Every other
/// name, and every name on Windows, opens through the <see cref="IFileSystem" /> as before.
/// </summary>
internal static partial class RawByteFileName
{
    /// <summary>The runtime's <c>O_WRONLY</c> (its PAL value, the same on Linux and macOS).</summary>
    internal const int WriteOnly = 0x0001;

    /// <summary>The runtime's <c>O_CLOEXEC</c>.</summary>
    internal const int CloseOnExec = 0x0010;

    /// <summary>The runtime's <c>O_CREAT</c>.</summary>
    internal const int Create = 0x0020;

    /// <summary>The runtime's <c>O_EXCL</c>.</summary>
    internal const int Exclusive = 0x0040;

    /// <summary>The runtime's <c>O_TRUNC</c>.</summary>
    internal const int Truncate = 0x0080;

    /// <summary>The status of each <c>errno</c> an <c>open</c> names specially; any other is an I/O error.</summary>
    private static readonly Dictionary<int, FileAccessStatus> StatusesByErrno = new()
    {
        [1] = FileAccessStatus.AccessDenied,
        [2] = FileAccessStatus.NotFound,
        [13] = FileAccessStatus.AccessDenied,
        [17] = FileAccessStatus.AlreadyExists,
        [20] = FileAccessStatus.NotADirectory,
        [21] = FileAccessStatus.IsDirectory,
    };

    /// <summary>
    /// Opens <paramref name="path" /> for writing: under its raw bytes when it holds an escaped
    /// byte off Windows, otherwise through <paramref name="fileSystem" />.
    /// </summary>
    /// <param name="fileSystem">Opens every name that needs no raw bytes.</param>
    /// <param name="path">The output file's name, as the runner resolved it.</param>
    /// <param name="mode">How existing content is treated.</param>
    /// <param name="createMode">The permissions a created file gets, before the umask.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open's answer.</returns>
    internal static ValueTask<FileOpenResult> OpenForWriteAsync(
        IFileSystem fileSystem,
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        CancellationToken cancellationToken) =>
        OpenForWriteAsync(fileSystem, path, mode, createMode, OperatingSystem.IsWindows(), OpenUnderRawBytes, cancellationToken);

    /// <summary>
    /// Opens <paramref name="path" /> with <paramref name="openRaw" /> when it holds an escaped byte
    /// and <paramref name="isWindows" /> is <see langword="false" />, otherwise with
    /// <paramref name="fileSystem" />.
    /// </summary>
    /// <param name="fileSystem">Opens every name that needs no raw bytes.</param>
    /// <param name="path">The output file's name.</param>
    /// <param name="mode">How existing content is treated.</param>
    /// <param name="createMode">The permissions a created file gets, before the umask.</param>
    /// <param name="isWindows">Whether this is Windows, where argument bytes are never escaped.</param>
    /// <param name="openRaw">Opens the name's NUL-terminated raw bytes.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open's answer.</returns>
    internal static ValueTask<FileOpenResult> OpenForWriteAsync(
        IFileSystem fileSystem,
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        bool isWindows,
        Func<byte[], FileWriteMode, UnixFileMode, FileOpenResult> openRaw,
        CancellationToken cancellationToken)
    {
        if (isWindows || !HoldsEscapedByte(path))
        {
            return fileSystem.OpenForWriteAsync(path, mode, createMode, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(openRaw(NullTerminatedBytes(path), mode, createMode));
    }

    /// <summary>Tells whether <paramref name="path" /> holds a byte escaped as U+DC80 to U+DCFF.</summary>
    /// <param name="path">A file name.</param>
    /// <returns><see langword="true" /> when the name needs its raw bytes.</returns>
    internal static bool HoldsEscapedByte(string path) => path.AsSpan().IndexOfAnyInRange('\uDC80', '\uDCFF') >= 0;

    /// <summary>Encodes <paramref name="path" /> with each escaped byte as itself, then a NUL.</summary>
    /// <param name="path">A file name.</param>
    /// <returns>The bytes <c>open</c> takes.</returns>
    internal static byte[] NullTerminatedBytes(string path) => InvalidUtf8ByteEscape.Encoding.GetBytes(path + "\0");

    /// <summary>
    /// The runtime's <c>open</c> flags for <paramref name="mode" />. It has no <c>O_APPEND</c>, so
    /// <see cref="FileWriteMode.Append" /> opens without truncating and the stream seeks to the end,
    /// as <see cref="FileMode.Append" /> does.
    /// </summary>
    /// <param name="mode">How existing content is treated.</param>
    /// <returns>The flags.</returns>
    internal static int OpenFlagsFor(FileWriteMode mode) =>
        WriteOnly | CloseOnExec | Create | mode switch
        {
            FileWriteMode.Truncate => Truncate,
            FileWriteMode.CreateNew => Exclusive,
            _ => 0,
        };

    /// <summary>
    /// The <see cref="FileAccessStatus" /> of a failed <c>open</c>'s <c>errno</c>, whose values
    /// these are on both Linux and macOS.
    /// </summary>
    /// <param name="errno">The error number.</param>
    /// <returns>The status.</returns>
    internal static FileAccessStatus StatusFor(int errno) => StatusesByErrno.GetValueOrDefault(errno, FileAccessStatus.IoError);

    /// <summary>Opens the NUL-terminated name <paramref name="path" /> with the runtime's <c>open</c>.</summary>
    /// <param name="path">The name's raw bytes and a NUL.</param>
    /// <param name="mode">How existing content is treated.</param>
    /// <param name="createMode">The permissions a created file gets, before the umask.</param>
    /// <returns>The open's answer.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0822: a thin adapter over an open only Linux and macOS take raw bytes in.")]
    private static FileOpenResult OpenUnderRawBytes(byte[] path, FileWriteMode mode, UnixFileMode createMode)
    {
        nint descriptor;
        int errno;
        unsafe
        {
            fixed (byte* name = path)
            {
                descriptor = Open(name, OpenFlagsFor(mode), (int)createMode);
                errno = Marshal.GetLastSystemError();
            }
        }

        if (descriptor < 0)
        {
            return FileOpenResult.Failed(StatusFor(errno));
        }

        FileStream stream = new(new SafeFileHandle(descriptor, ownsHandle: true), FileAccess.Write);
        if (mode == FileWriteMode.Append)
        {
            stream.Seek(0, SeekOrigin.End);
        }

        return FileOpenResult.Opened(stream, stream.Length, null);
    }

    /// <summary>
    /// The runtime's non-variadic <c>open</c> shim, which every .NET build on Linux and macOS ships
    /// and Native AOT links in. libc's own <c>open</c> is variadic, which a P/Invoke cannot call
    /// correctly on Apple silicon. Its arguments are blittable, so no marshalling stub is generated;
    /// the caller reads <c>errno</c> straight after, as a <c>SetLastError</c> stub would.
    /// </summary>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Open")]
    private static unsafe partial nint Open(byte* path, int flags, int mode);
}
