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
/// <see cref="FileStream" /> over the process's own standard-output handle, with no buffer,
/// which reports those failures as an <see cref="IOException" />; a closed one becomes a
/// <see cref="ClosedStandardOutputStream" />. A console is still opened with
/// <see cref="System.Console.OpenStandardOutput()" />, unchanged.
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
            System.Console.IsOutputRedirected,
            System.Console.OpenStandardOutput,
            ReadStandardOutputHandleValue(OperatingSystem.IsWindows(), GetStandardOutputHandleOnWindows));

    /// <summary>
    /// Opens standard output as a console stream when it is a console, and over its handle
    /// otherwise.
    /// </summary>
    /// <param name="isOutputRedirected">Whether standard output is anything but a console.</param>
    /// <param name="openConsoleStream">Opens standard output as a console stream.</param>
    /// <param name="standardOutputHandleValue">The raw standard-output handle, or file descriptor.</param>
    /// <returns>The stream a transfer to standard output writes through.</returns>
    internal static Stream Open(
        bool isOutputRedirected,
        Func<Stream> openConsoleStream,
        nint standardOutputHandleValue) =>
        isOutputRedirected ? OpenHandle(standardOutputHandleValue) : openConsoleStream();

    /// <summary>
    /// Opens a raw standard-output handle as an unbuffered stream it does not own.
    /// </summary>
    /// <param name="handleValue">The handle, or the file descriptor on Linux and macOS.</param>
    /// <returns>
    /// A <see cref="ClosedStandardOutputStream" /> when <paramref name="handleValue" /> is
    /// zero or <c>-1</c>, which is how a closed standard output reads, or when it cannot be
    /// opened; otherwise a <see cref="FileStream" /> with no buffer over it, so each write
    /// reaches the handle before the call returns.
    /// </returns>
    internal static Stream OpenHandle(nint handleValue) => OpenHandle(handleValue, OpenUnbufferedFileStream);

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
    /// Opens <paramref name="handle" /> for writing with no buffer, so each write reaches the
    /// handle before the call returns and a failed write throws at once.
    /// </summary>
    /// <param name="handle">The standard-output handle.</param>
    /// <returns>The unbuffered stream.</returns>
    private static FileStream OpenUnbufferedFileStream(SafeFileHandle handle) =>
        new(handle, FileAccess.Write, bufferSize: 0);

    /// <summary>
    /// Reads the raw standard-output handle for the platform.
    /// </summary>
    /// <param name="isWindows">Whether the process runs on Windows.</param>
    /// <param name="getStandardOutputHandleOnWindows">Calls <c>GetStdHandle</c> for standard output.</param>
    /// <returns>
    /// The Windows handle, or <see cref="UnixStandardOutputFileDescriptor" /> elsewhere.
    /// </returns>
    internal static nint ReadStandardOutputHandleValue(bool isWindows, Func<nint> getStandardOutputHandleOnWindows) =>
        isWindows ? getStandardOutputHandleOnWindows() : UnixStandardOutputFileDescriptor;

    /// <summary>
    /// Calls <c>GetStdHandle(STD_OUTPUT_HANDLE)</c>.
    /// </summary>
    /// <returns>The standard-output handle; zero or <c>-1</c> when it is closed.</returns>
    private static nint GetStandardOutputHandleOnWindows() => GetStdHandle(WindowsStandardOutputHandleId);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetStdHandle(int standardHandleId);
}
