using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Curl.Core.FileSystem;

/// <summary>
/// Sets a file's last-write time from Unix seconds that .NET's <see cref="DateTime" />
/// cannot hold - past 9999-12-31T23:59:59Z or before 0001-01-01 - with the operating
/// system's own call: <c>SetFileTime</c> on Windows, which reaches year 30828, and
/// <c>utimes</c> elsewhere, as curl 8.21.0's <c>setfiletime</c> does (ADR-0410, BL-1425).
/// </summary>
/// <remarks>
/// <c>utimes</c> sets the access time to the same value, as curl's does. Opening the file
/// on Windows throws what <see cref="File.OpenHandle" /> throws, for the caller's
/// <see cref="FileOpenFailure" /> to read; a <c>SetFileTime</c> or <c>utimes</c> failure
/// returns <see langword="false" /> with the Win32 error code or <c>errno</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin adapter over SetFileTime and utimes, tested on each platform by PhysicalFileSystemTests.")]
internal static partial class NativeFileTimeSetter
{
    /// <summary>The <c>FILETIME</c> of 1970-01-01T00:00:00Z: 100-nanosecond intervals since 1601.</summary>
    private const long UnixEpochFileTime = 116_444_736_000_000_000;

    /// <summary>The <c>FILETIME</c> intervals in one second.</summary>
    private const long FileTimeIntervalsPerSecond = 10_000_000;

    /// <summary>
    /// Sets the last-write time of the file at <paramref name="path" />.
    /// </summary>
    /// <param name="path">The operating-system path of an existing file.</param>
    /// <param name="unixSeconds">The time in seconds since 1970-01-01T00:00:00Z.</param>
    /// <param name="errorCode">Zero when set; otherwise the Win32 error code or <c>errno</c>.</param>
    /// <returns><see langword="true" /> when the time was set.</returns>
    internal static bool TrySetLastWriteUnixSeconds(string path, long unixSeconds, out int errorCode)
    {
        bool set = OperatingSystem.IsWindows()
            ? TrySetOnWindows(path, unixSeconds)
            : Utimes(path, [new TimeValue(unixSeconds), new TimeValue(unixSeconds)]) == 0;
        errorCode = set ? 0 : Marshal.GetLastPInvokeError();

        return set;
    }

    private static bool TrySetOnWindows(string path, long unixSeconds)
    {
        using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        long lastWrite = unchecked((unixSeconds * FileTimeIntervalsPerSecond) + UnixEpochFileTime);

        return SetFileTime(file, IntPtr.Zero, IntPtr.Zero, in lastWrite);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetFileTime(
        SafeFileHandle file,
        IntPtr creationTime,
        IntPtr lastAccessTime,
        in long lastWriteTime);

    [LibraryImport("libc", EntryPoint = "utimes", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Utimes(string path, [In] TimeValue[] times);

    /// <summary>
    /// A 64-bit <c>struct timeval</c>: seconds and microseconds, each a 64-bit slot on
    /// Linux and macOS alike (macOS pads its 32-bit microseconds to 8 bytes).
    /// </summary>
    /// <param name="seconds">The whole seconds.</param>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct TimeValue(long seconds)
    {
        private readonly long seconds = seconds;
        private readonly long microseconds = 0;
    }
}
