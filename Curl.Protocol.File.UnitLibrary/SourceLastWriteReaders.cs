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
