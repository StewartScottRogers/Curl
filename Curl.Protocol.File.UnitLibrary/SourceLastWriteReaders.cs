using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Curl.Protocol.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace Curl.Protocol.File;

/// <summary>
/// Reads an opened <c>file://</c> source's last-write time from its handle in Unix seconds,
/// where <see cref="FileOpenResult.LastWriteTimeUtc" />, a <see cref="DateTimeOffset" />,
/// cannot hold it: a time past 9999-12-31T23:59:59Z (ADR-0410, BL-1423).
/// </summary>
internal interface ISourceLastWriteReader
{
    /// <summary>
    /// Reads the last-write time of the handle behind <paramref name="source" />.
    /// </summary>
    /// <param name="source">The opened source.</param>
    /// <returns>
    /// The time in seconds since 1970-01-01T00:00:00Z, truncated to the whole second, or
    /// <see langword="null" /> when this reader cannot read one from that stream.
    /// </returns>
    long? ReadLastWriteUnixSeconds(Stream source);
}

/// <summary>
/// The reader for a platform with no raw read of its own yet: it reads nothing, so the
/// time <see cref="FileOpenResult.LastWriteTimeUtc" /> carries is the only one.
/// </summary>
internal sealed class NoRawSourceLastWriteReader : ISourceLastWriteReader
{
    /// <inheritdoc />
    public long? ReadLastWriteUnixSeconds(Stream source) => null;
}

/// <summary>
/// Reads a Windows <see cref="FileStream" />'s raw <c>FILETIME</c> with
/// <c>GetFileTime</c>, which reaches year 30828 where .NET's <see cref="DateTime" /> stops
/// at 9999.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class Win32SourceLastWriteReader : ISourceLastWriteReader
{
    /// <summary>The <c>FILETIME</c> of 1970-01-01T00:00:00Z: 100-nanosecond intervals since 1601.</summary>
    private const long UnixEpochFileTime = 116_444_736_000_000_000;

    /// <summary>The <c>FILETIME</c> intervals in one second.</summary>
    private const long FileTimeIntervalsPerSecond = 10_000_000;

    /// <inheritdoc />
    /// <remarks>
    /// <see langword="null" /> for a stream that is not a <see cref="FileStream" />. A handle
    /// <c>GetFileTime</c> refuses leaves the <c>FILETIME</c> at zero, as a device reports
    /// one: 1601, which the handler never prefers to the open's own time.
    /// </remarks>
    public long? ReadLastWriteUnixSeconds(Stream source) =>
        source is FileStream fileStream ? ReadLastWriteUnixSeconds(fileStream.SafeFileHandle) : null;

    /// <summary>
    /// Reads a handle's last-write <c>FILETIME</c> in whole Unix seconds.
    /// </summary>
    /// <param name="file">The handle.</param>
    /// <returns>The time in seconds since 1970, or those of 1601 when unread.</returns>
    private static long ReadLastWriteUnixSeconds(SafeFileHandle file)
    {
        _ = GetFileTime(file, IntPtr.Zero, IntPtr.Zero, out long lastWrite);

        return (lastWrite / FileTimeIntervalsPerSecond) - (UnixEpochFileTime / FileTimeIntervalsPerSecond);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileTime(
        SafeFileHandle file,
        IntPtr creationTime,
        IntPtr lastAccessTime,
        out long lastWriteTime);
}

/// <summary>
/// Reads a Linux or macOS <see cref="FileStream" />'s <c>st_mtime</c> seconds from its open
/// descriptor, whole and 64-bit, where .NET's <see cref="DateTime" /> stops at 9999: with
/// <c>statx</c> on Linux and <c>fgetattrlist</c>'s <c>ATTR_CMN_MODTIME</c> on macOS, whose
/// buffers lay out the same on every architecture, unlike <c>struct stat</c> (BL-1790).
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin adapter over statx and fgetattrlist, tested on Linux and macOS by FileProtocolHandlerSourceLastWriteTests.")]
internal sealed partial class PosixSourceLastWriteReader : ISourceLastWriteReader
{
    /// <summary><c>AT_EMPTY_PATH</c>: <c>statx</c> reads the descriptor itself.</summary>
    private const int AtEmptyPath = 0x1000;

    /// <summary><c>STATX_MTIME</c>: the modification time.</summary>
    private const uint StatxMTime = 0x40;

    /// <summary><c>ATTR_BIT_MAP_COUNT</c>: the bitmaps in a macOS <c>attrlist</c>.</summary>
    private const ushort AttributeBitmapCount = 5;

    /// <summary><c>ATTR_CMN_MODTIME</c>: the modification time as a <c>timespec</c>.</summary>
    private const uint CommonModificationTime = 0x400;

    /// <inheritdoc />
    /// <remarks>
    /// <see langword="null" /> for a stream that is not a <see cref="FileStream" />, and for a
    /// descriptor the call refuses or reads no modification time for, so the handler keeps the
    /// open's own time.
    /// </remarks>
    public long? ReadLastWriteUnixSeconds(Stream source) =>
        source is FileStream fileStream ? ReadLastWriteUnixSeconds(fileStream.SafeFileHandle) : null;

    /// <summary>Reads a descriptor's modification time in whole Unix seconds.</summary>
    /// <param name="file">The descriptor.</param>
    /// <returns>The seconds since 1970, or <see langword="null" /> when unread.</returns>
    private static long? ReadLastWriteUnixSeconds(SafeFileHandle file)
    {
        bool added = false;
        try
        {
            file.DangerousAddRef(ref added);
            int descriptor = (int)file.DangerousGetHandle();

            return OperatingSystem.IsMacOS() ? ReadDarwin(descriptor) : ReadLinux(descriptor);
        }
        finally
        {
            if (added)
            {
                file.DangerousRelease();
            }
        }
    }

    /// <summary>Reads <c>stx_mtime.tv_sec</c> with <c>statx</c>.</summary>
    /// <param name="descriptor">The open descriptor.</param>
    /// <returns>The seconds, or <see langword="null" /> when unread.</returns>
    private static long? ReadLinux(int descriptor) =>
        Statx(descriptor, string.Empty, AtEmptyPath, StatxMTime, out StatxBuffer buffer) == 0
            && (buffer.Mask & StatxMTime) != 0
            ? buffer.ModificationSeconds
            : null;

    /// <summary>Reads <c>ATTR_CMN_MODTIME</c>'s <c>tv_sec</c> with <c>fgetattrlist</c>.</summary>
    /// <param name="descriptor">The open descriptor.</param>
    /// <returns>The seconds, or <see langword="null" /> when unread.</returns>
    private static long? ReadDarwin(int descriptor)
    {
        var request = new AttributeList { BitmapCount = AttributeBitmapCount, CommonAttributes = CommonModificationTime };

        return GetAttributeList(descriptor, ref request, out ModificationTimeBuffer buffer, (nuint)Marshal.SizeOf<ModificationTimeBuffer>(), 0) == 0
            ? buffer.ModificationSeconds
            : null;
    }

    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Statx(int directory, string path, int flags, uint mask, out StatxBuffer buffer);

    [LibraryImport("libc", EntryPoint = "fgetattrlist")]
    private static partial int GetAttributeList(
        int descriptor,
        ref AttributeList request,
        out ModificationTimeBuffer buffer,
        nuint bufferSize,
        nuint options);

    /// <summary>Linux's <c>struct statx</c>, 256 bytes; only the fields read are named.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct StatxBuffer
    {
        /// <summary><c>stx_mask</c>: the fields filled in.</summary>
        [FieldOffset(0)]
        public uint Mask;

        /// <summary><c>stx_mtime.tv_sec</c>.</summary>
        [FieldOffset(112)]
        public long ModificationSeconds;
    }

    /// <summary>macOS's <c>struct attrlist</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeList
    {
        /// <summary><c>bitmapcount</c>.</summary>
        public ushort BitmapCount;

        /// <summary><c>reserved</c>.</summary>
        public ushort Reserved;

        /// <summary><c>commonattr</c>.</summary>
        public uint CommonAttributes;

        /// <summary><c>volattr</c>.</summary>
        public uint VolumeAttributes;

        /// <summary><c>dirattr</c>.</summary>
        public uint DirectoryAttributes;

        /// <summary><c>fileattr</c>.</summary>
        public uint FileAttributes;

        /// <summary><c>forkattr</c>.</summary>
        public uint ForkAttributes;
    }

    /// <summary>
    /// <c>fgetattrlist</c>'s answer for <c>ATTR_CMN_MODTIME</c> alone: a 4-byte length, then
    /// the <c>timespec</c> on the 4-byte boundary attributes are packed to.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct ModificationTimeBuffer
    {
        /// <summary>The <c>timespec</c>'s <c>tv_sec</c>.</summary>
        [FieldOffset(4)]
        public long ModificationSeconds;
    }
}
