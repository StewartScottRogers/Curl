using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Curl.Cli;

/// <summary>
/// Reads a file's last write time as curl 8.21.0's Windows build does (<c>src/tool_filetime.c</c>):
/// <c>CreateFile</c> with <c>FILE_READ_ATTRIBUTES</c>, every share mode and <c>OPEN_EXISTING</c>,
/// then <c>GetFileTime</c>. The same calls fail the same way, so a directory fails in
/// <c>CreateFile</c> with 0x00000005, <c>con</c> in <c>CreateFile</c> with 0x00000057 and
/// <c>nul</c> in <c>GetFileTime</c> with 0x00000057.
/// </summary>
internal static class WindowsFileTimeReader
{
    private const uint FileReadAttributes = 0x80;

    private const uint FileShareAll = 0x7;

    private const uint OpenExisting = 3;

    /// <summary>
    /// Reads the last write time of <paramref name="path"/> in UTC.
    /// </summary>
    /// <param name="path">The file to look up.</param>
    /// <returns>The file's last write time in UTC.</returns>
    /// <exception cref="FileTimeLookupException">Either call failed; it names the call and its error code.</exception>
    public static DateTime ReadLastWriteTimeUtc(string path)
    {
        using SafeFileHandle handle = new(CreateFileW(path, FileReadAttributes, FileShareAll, 0, OpenExisting, 0, 0), ownsHandle: true);
        if (handle.IsInvalid)
        {
            throw new FileTimeLookupException("CreateFile", Marshal.GetLastPInvokeError());
        }

        if (!GetFileTime(handle, out _, out _, out long lastWriteTime))
        {
            throw new FileTimeLookupException("GetFileTime", Marshal.GetLastPInvokeError());
        }

        return DateTime.FromFileTimeUtc(lastWriteTime);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint CreateFileW(string fileName, uint desiredAccess, uint shareMode, nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileTime(SafeFileHandle file, out long creationTime, out long lastAccessTime, out long lastWriteTime);
}
