using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Curl.Console;

/// <summary>
/// Writes to a standard handle at its shared file offset: <c>WriteFile</c> with no
/// <c>OVERLAPPED</c> on Windows, elsewhere <c>write(2)</c> through the runtime's own
/// <c>SystemNative_Write</c> shim in <c>libSystem.Native</c>, which retries on <c>EINTR</c>
/// (BL-3937, ADR-0945).
/// </summary>
/// <remarks>
/// A thin adapter over the two calls; <see cref="SharedOffsetHandleStream" /> holds the logic.
/// The Unix call cannot run on Windows, so per ADR-0083 the type is left out of the Windows
/// coverage measure; <c>StandardOutputOpenerTests</c> drive it on every platform.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0945: a thin WriteFile and write(2) adapter.")]
internal static partial class NativeSharedOffsetWrite
{
    /// <summary>
    /// Writes <paramref name="bytes" />, or a leading part of them, at the shared offset of
    /// <paramref name="handleValue" />.
    /// </summary>
    /// <param name="handleValue">The handle, or the file descriptor on Linux and macOS.</param>
    /// <param name="bytes">The bytes to write.</param>
    /// <returns>How many bytes were written.</returns>
    /// <exception cref="IOException">The write failed (a reader that has gone, a full disk).</exception>
    internal static int Write(nint handleValue, ReadOnlySpan<byte> bytes)
    {
        int written;
        bool succeeded;
        if (OperatingSystem.IsWindows())
        {
            succeeded = WriteFile(handleValue, bytes, bytes.Length, out written, 0);
        }
        else
        {
            written = WriteDescriptor(handleValue, bytes, bytes.Length);
            succeeded = written >= 0;
        }

        if (!succeeded)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException(Marshal.GetPInvokeErrorMessage(error), error);
        }

        return written;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WriteFile(nint handle, ReadOnlySpan<byte> buffer, int count, out int written, nint overlapped);

    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_Write", SetLastError = true)]
    private static partial int WriteDescriptor(nint descriptor, ReadOnlySpan<byte> buffer, int count);
}
