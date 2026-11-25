using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Curl.Core.FileSystem;

/// <summary>
/// Whether an opened file is one a <see cref="Curl.Protocol.Abstractions.FileWriteMode.Replace" />
/// open replaces, as libcurl's <c>Curl_fopen</c> asks <c>fstat</c>.
/// </summary>
public sealed partial class PhysicalFileSystem
{
    /// <summary><c>S_IFMT</c>, the bits of a <c>st_mode</c> that hold the file's type.</summary>
    private const int FileTypeBits = 0xF000;

    /// <summary><c>S_IFREG</c>, the type of a regular file.</summary>
    private const int RegularFileType = 0x8000;

    /// <summary>The permission, set-ID and sticky bits of a <c>st_mode</c>.</summary>
    private const int PermissionBits = 0x0FFF;

    /// <summary>
    /// The mode reader a platform uses: none on Windows, which writes every file in place, and
    /// the runtime's <c>fstat</c> shim elsewhere.
    /// </summary>
    /// <param name="isWindows">Whether this is Windows.</param>
    /// <returns>The reader, or <see langword="null" /> on Windows.</returns>
    internal static Func<SafeFileHandle, UnixFileMode?>? ReplaceableModeReaderFor(bool isWindows) =>
        isWindows ? null : ReadReplaceableMode;

    /// <summary>The permission bits of a regular file's <c>st_mode</c>.</summary>
    /// <param name="status">The <c>st_mode</c>, the same values on Linux and macOS.</param>
    /// <returns>The bits, or <see langword="null" /> when the mode is not of a regular file.</returns>
    internal static UnixFileMode? ReplaceableModeIn(int status) =>
        (status & FileTypeBits) == RegularFileType ? (UnixFileMode)(status & PermissionBits) : null;

    /// <summary>Reads the mode of an opened handle with the runtime's <c>fstat</c>.</summary>
    /// <param name="handle">The opened file.</param>
    /// <returns>As <see cref="ReplaceableModeIn(int)" />; <see langword="null" /> when <c>fstat</c> fails.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-1368: a thin adapter over an fstat only Linux and macOS have.")]
    private static unsafe UnixFileMode? ReadReplaceableMode(SafeFileHandle handle)
    {
        // The runtime's FileStatus starts with two 32-bit fields, Flags and Mode, and is under
        // 256 bytes whole; the 64-bit elements align it.
        long* status = stackalloc long[32];

        // The caller holds the stream open, so the descriptor outlives the call.
        return SystemNativeFStat(handle.DangerousGetHandle(), status) == 0 ? ReplaceableModeIn(((int*)status)[1]) : null;
    }

    /// <summary>
    /// The runtime's <c>fstat</c> shim, whose layout is the same on every platform. Its arguments
    /// are blittable, so no marshalling stub is generated.
    /// </summary>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_FStat")]
    private static unsafe partial int SystemNativeFStat(nint descriptor, long* status);
}
