using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Curl.Console;

/// <summary>
/// Opens standard output as a stream whose writes fail when standard output is closed or
/// its reader has gone, so curl's exit 23 comes out of the real executable.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Console.OpenStandardOutput()" /> hides both failures: it returns
/// <see cref="Stream.Null" /> for a closed standard output, and its stream reports a write to
/// a pipe whose reader has gone as a success (<c>ERROR_BROKEN_PIPE</c> and
/// <c>ERROR_NO_DATA</c> on Windows, <c>EPIPE</c> on Linux).
/// </para>
/// <para>
/// So a redirected standard output - a file, a pipe, a device - is opened as a
/// <see cref="SharedOffsetHandleStream" /> over the process's own standard-output handle, with
/// no buffer, which reports those failures as an <see cref="IOException" /> and writes at the
/// handle's shared file offset, as curl's <c>fwrite</c> does, not at a position of its own as a
/// <see cref="FileStream" /> would (BL-3937, ADR-0944); a closed one becomes a
/// <see cref="ClosedStandardOutputStream" />. On Windows a console is still opened with
/// <see cref="System.Console.OpenStandardOutput()" />; elsewhere a terminal is opened over
/// descriptor 1 too, because a write through .NET's console stream first writes terminfo's
/// <c>keypad_xmit</c> to the terminal (CGR-115, ADR-0866).
/// </para>
/// </remarks>
internal static class StandardOutputOpener
{
    /// <summary>
    /// <c>STD_OUTPUT_HANDLE</c>, the argument <c>GetStdHandle</c> takes for standard output.
    /// </summary>
    internal const int WindowsStandardOutputHandleId = -11;

    /// <summary>
    /// The file descriptor of standard output on Linux and macOS.
    /// </summary>
    internal const int UnixStandardOutputFileDescriptor = 1;

    /// <summary>
    /// Opens this process's standard output.
    /// </summary>
    /// <returns>The stream a transfer to standard output writes through.</returns>
    internal static Stream Open() =>
        Open(
            OpensOverHandle(System.Console.IsOutputRedirected, OperatingSystem.IsWindows()),
            System.Console.OpenStandardOutput,
            ReadStandardOutputHandleValue(OperatingSystem.IsWindows(), GetStandardOutputHandleOnWindows, NativeInheritedFileDescriptor.IsInherited));

    /// <summary>
    /// Tells whether standard output is opened over its handle rather than as a console
    /// stream: when it is redirected, and always off Windows, where a write through .NET's
    /// console stream writes terminfo's <c>keypad_xmit</c> (<c>ESC[?1h ESC=</c>) to the terminal
    /// first (CGR-115, ADR-0866).
    /// </summary>
    /// <param name="isOutputRedirected">Whether standard output is anything but a console.</param>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <returns>Whether to open standard output over its handle.</returns>
    internal static bool OpensOverHandle(bool isOutputRedirected, bool runsOnWindows) =>
        isOutputRedirected || !runsOnWindows;

    /// <summary>
    /// Opens standard output over its handle when <paramref name="opensOverHandle" /> is
    /// set, and as a console stream otherwise.
    /// </summary>
    /// <param name="opensOverHandle">Whether to open standard output over its handle (see <see cref="OpensOverHandle" />).</param>
    /// <param name="openConsoleStream">Opens standard output as a console stream.</param>
    /// <param name="standardOutputHandleValue">The raw standard-output handle, or file descriptor.</param>
    /// <returns>The stream a transfer to standard output writes through.</returns>
    internal static Stream Open(
        bool opensOverHandle,
        Func<Stream> openConsoleStream,
        nint standardOutputHandleValue) =>
        opensOverHandle ? OpenHandle(standardOutputHandleValue) : openConsoleStream();

    /// <summary>
    /// Opens a raw standard-output handle as an unbuffered stream it does not own.
    /// </summary>
    /// <param name="handleValue">The handle, or the file descriptor on Linux and macOS.</param>
    /// <returns>
    /// A <see cref="ClosedStandardOutputStream" /> when <paramref name="handleValue" /> is
    /// zero or <c>-1</c>, which is how a closed standard output reads, or when it cannot be
    /// opened; otherwise a <see cref="SharedOffsetHandleStream" /> with no buffer over it, so each
    /// write reaches the handle, at its shared file offset, before the call returns.
    /// </returns>
    internal static Stream OpenHandle(nint handleValue) => OpenHandle(handleValue, OpenUnbufferedSharedOffsetStream);

    /// <summary>
    /// Opens a raw standard-output handle with <paramref name="openHandleStream" />, as a
    /// handle it does not own.
    /// </summary>
    /// <param name="handleValue">The handle, or the file descriptor on Linux and macOS.</param>
    /// <param name="openHandleStream">Opens the stream over the handle.</param>
    /// <returns>
    /// A <see cref="ClosedStandardOutputStream" /> when <paramref name="handleValue" /> is
    /// zero or <c>-1</c>, or when <paramref name="openHandleStream" /> throws an
    /// <see cref="IOException" /> (as it does on Windows for a handle that is not open);
    /// otherwise the stream it opened.
    /// </returns>
    internal static Stream OpenHandle(nint handleValue, Func<SafeFileHandle, Stream> openHandleStream)
    {
        if (handleValue == 0 || handleValue == -1)
        {
            return new ClosedStandardOutputStream();
        }

        try
        {
            return openHandleStream(new SafeFileHandle(handleValue, ownsHandle: false));
        }
        catch (IOException)
        {
            return new ClosedStandardOutputStream();
        }
    }

    /// <summary>
    /// Opens <paramref name="handle" /> for writing with no buffer, at the handle's shared file
    /// offset, so each write reaches the handle before the call returns, a failed write throws
    /// at once, and a file shared with the shell or with standard error gets the bytes where
    /// curl's would go (BL-3937, ADR-0944).
    /// </summary>
    /// <param name="handle">The standard-output handle.</param>
    /// <returns>The unbuffered stream.</returns>
    private static SharedOffsetHandleStream OpenUnbufferedSharedOffsetStream(SafeFileHandle handle) =>
        new(handle.DangerousGetHandle(), NativeSharedOffsetWrite.Write);

    /// <summary>
    /// Reads the raw standard-output handle for the platform.
    /// </summary>
    /// <param name="isWindows">Whether the process runs on Windows.</param>
    /// <param name="getStandardOutputHandleOnWindows">Calls <c>GetStdHandle</c> for standard output.</param>
    /// <param name="isInheritedDescriptor">Tells whether a descriptor is open and was inherited.</param>
    /// <returns>
    /// The Windows handle; elsewhere <see cref="UnixStandardOutputFileDescriptor" />, or <c>-1</c>
    /// (closed) when descriptor 1 was not inherited, because the shell closed it (<c>1&gt;&amp;-</c>)
    /// and the runtime may have taken the number for one of its own pipes. curl then writes to the
    /// read-only <c>/dev/null</c> its <c>main_checkfds</c> opens there, so its write fails and it
    /// exits 23 (BL-3936, ADR-0869).
    /// </returns>
    internal static nint ReadStandardOutputHandleValue(bool isWindows, Func<nint> getStandardOutputHandleOnWindows, Func<int, bool> isInheritedDescriptor)
    {
        if (isWindows)
        {
            return getStandardOutputHandleOnWindows();
        }

        return isInheritedDescriptor(UnixStandardOutputFileDescriptor) ? UnixStandardOutputFileDescriptor : -1;
    }

    /// <summary>
    /// Calls <c>GetStdHandle(STD_OUTPUT_HANDLE)</c>.
    /// </summary>
    /// <returns>The standard-output handle; zero or <c>-1</c> when it is closed.</returns>
    private static nint GetStandardOutputHandleOnWindows() => GetStdHandle(WindowsStandardOutputHandleId);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetStdHandle(int standardHandleId);
}
