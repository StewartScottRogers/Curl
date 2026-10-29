using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Curl.Cli;
using Curl.Protocol.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace Curl.Console;

/// <summary>
/// Opens an output file whose name holds argument bytes that are not valid UTF-8 under those raw
/// bytes, as curl 8.18.0 does on Linux and macOS (CGR-016, BL-3853, ADR-0920): <c>-o $'\xff.txt'</c>
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

    /// <summary>POSIX <c>S_IFREG</c>: the file type of a regular file.</summary>
    internal const int RegularFileType = 0x8000;

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
    /// Opens <paramref name="path" /> with <paramref name="openRaw" /> when there is one and the name
    /// holds an escaped byte, otherwise with <paramref name="fileSystem" />.
    /// </summary>
    /// <param name="fileSystem">Opens every name that needs no raw bytes.</param>
    /// <param name="path">The output file's name.</param>
    /// <param name="mode">How existing content is treated.</param>
    /// <param name="createMode">The permissions a created file gets, before the umask.</param>
    /// <param name="openRaw">
    /// Opens the name's NUL-terminated raw bytes; <see langword="null" /> on Windows, where argument
    /// bytes are never escaped, and wherever <paramref name="fileSystem" /> must answer every open, as
    /// in tests (<see cref="IOutputPaths.OpenUnderRawBytes" />, BL-4042).
    /// </param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open's answer.</returns>
    internal static ValueTask<FileOpenResult> OpenForWriteAsync(
        IFileSystem fileSystem,
        string path,
        FileWriteMode mode,
        UnixFileMode createMode,
        Func<byte[], FileWriteMode, UnixFileMode, FileOpenResult>? openRaw,
        CancellationToken cancellationToken)
    {
        if (openRaw is null || !HoldsEscapedByte(path))
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
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0920: a thin adapter over an open only Linux and macOS take raw bytes in.")]
    internal static FileOpenResult OpenUnderRawBytes(byte[] path, FileWriteMode mode, UnixFileMode createMode)
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
    /// Makes the directory named by the NUL-terminated raw bytes <paramref name="path" /> with the
    /// runtime's <c>mkdir</c>, as curl's <c>--create-dirs</c> does (BL-3868), with the mode
    /// <see cref="Directory.CreateDirectory(string)" /> gives every other name.
    /// </summary>
    /// <param name="path">The name's raw bytes and a NUL.</param>
    /// <returns>0 when the directory was made, otherwise <c>mkdir</c>'s <c>errno</c>.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0920: a thin adapter over a mkdir only Linux and macOS take raw bytes in.")]
    internal static int MakeDirectoryUnderRawBytes(byte[] path)
    {
        const int AllPermissions = 0x1FF;
        unsafe
        {
            fixed (byte* name = path)
            {
                return MakeDirectory(name, AllPermissions) == 0 ? 0 : Marshal.GetLastSystemError();
            }
        }
    }

    /// <summary>
    /// The runtime's <c>mkdir</c> shim, which every .NET build on Linux and macOS ships and Native
    /// AOT links in; as with <see cref="Open" />, the caller reads <c>errno</c> straight after.
    /// </summary>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_MkDir")]
    private static unsafe partial int MakeDirectory(byte* path, int mode);

    /// <summary>
    /// Stats and deletes files under the raw bytes of their names, as curl's <c>--skip-existing</c>
    /// and <c>--remove-on-error</c> do on Linux and macOS (BL-3947).
    /// </summary>
    /// <param name="FileTypeOf">
    /// Gives the <c>S_IFMT</c> bits of the NUL-terminated name's <c>stat</c> mode, or -1 when
    /// <c>stat</c> fails.
    /// </param>
    /// <param name="Remove">Deletes the NUL-terminated name with <c>unlink</c>; <see langword="true" /> when it went.</param>
    internal sealed record FileOperations(Func<byte[], int> FileTypeOf, Func<byte[], bool> Remove)
    {
        /// <summary>The runtime's <c>stat</c> and <c>unlink</c> shims.</summary>
        internal static FileOperations Native { get; } = new(FileTypeUnderRawBytes, RemoveUnderRawBytes);
    }

    /// <summary>
    /// The file type bits of the <c>stat</c> mode of the file named by the NUL-terminated raw bytes
    /// <paramref name="path" />, following symbolic links.
    /// </summary>
    /// <param name="path">The name's raw bytes and a NUL.</param>
    /// <returns>The mode's <c>S_IFMT</c> bits, or -1 when <c>stat</c> fails.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0920: a thin adapter over a stat only Linux and macOS take raw bytes in.")]
    internal static int FileTypeUnderRawBytes(byte[] path)
    {
        const int FileTypeMask = 0xF000;
        RawFileStatus status;
        unsafe
        {
            fixed (byte* name = path)
            {
                return Stat(name, &status) == 0 ? status.Mode & FileTypeMask : -1;
            }
        }
    }

    /// <summary>Deletes the file named by the NUL-terminated raw bytes <paramref name="path" /> with <c>unlink</c>.</summary>
    /// <param name="path">The name's raw bytes and a NUL.</param>
    /// <returns><see langword="true" /> when the file was deleted.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0920: a thin adapter over an unlink only Linux and macOS take raw bytes in.")]
    internal static bool RemoveUnderRawBytes(byte[] path)
    {
        unsafe
        {
            fixed (byte* name = path)
            {
                return Unlink(name) == 0;
            }
        }
    }

    /// <summary>The runtime's <c>stat</c> shim, which follows symbolic links.</summary>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Stat")]
    private static unsafe partial int Stat(byte* path, RawFileStatus* status);

    /// <summary>The runtime's <c>unlink</c> shim.</summary>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Unlink")]
    private static unsafe partial int Unlink(byte* path);

    /// <summary>
    /// The head of the runtime's <c>FileStatus</c> record: its flags and mode, padded past the
    /// record's full length so the shim's write stays inside it.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 256)]
    private struct RawFileStatus
    {
        public int Flags;

        public int Mode;
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
