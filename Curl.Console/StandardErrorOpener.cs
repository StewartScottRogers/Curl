namespace Curl.Console;

/// <summary>
/// Opens standard error without writing anything to the terminal curl did not write.
/// </summary>
/// <remarks>
/// On Windows standard error is opened with <see cref="System.Console.OpenStandardError()" />.
/// Elsewhere it is opened over descriptor 2 as <see cref="StandardOutputOpener" /> opens
/// descriptor 1, because a write through .NET's console stream there first initialises the
/// terminfo console, which writes <c>keypad_xmit</c> (<c>ESC[?1h ESC=</c>) to the terminal and
/// never resets it (CGR-115, ADR-0866). When descriptor 2 was not inherited, because the shell
/// closed it (<c>2&gt;&amp;-</c>) and the runtime took the number for one of its own pipes, it is
/// replaced by <see cref="Stream.Null" />, as curl writes to the <c>/dev/null</c> its
/// <c>main_checkfds</c> opens there (BL-3824, ADR-0869).
/// </remarks>
internal static class StandardErrorOpener
{
    /// <summary>
    /// The file descriptor of standard error on Linux and macOS.
    /// </summary>
    internal const int UnixStandardErrorFileDescriptor = 2;

    /// <summary>
    /// Opens this process's standard error.
    /// </summary>
    /// <returns>The stream standard error is written through.</returns>
    internal static Stream Open() =>
        Open(OperatingSystem.IsWindows(), System.Console.OpenStandardError, StandardOutputOpener.OpenHandle, NativeInheritedFileDescriptor.IsInherited);

    /// <summary>
    /// Opens standard error as a console stream on Windows and over descriptor 2 elsewhere, or as
    /// <see cref="Stream.Null" /> when descriptor 2 was not inherited.
    /// </summary>
    /// <param name="runsOnWindows">Whether the process runs on Windows.</param>
    /// <param name="openConsoleStream">Opens standard error as a console stream.</param>
    /// <param name="openHandle">Opens a raw descriptor as an unbuffered stream it does not own.</param>
    /// <param name="isInheritedDescriptor">Tells whether a descriptor is open and was inherited.</param>
    /// <returns>The stream standard error is written through.</returns>
    internal static Stream Open(bool runsOnWindows, Func<Stream> openConsoleStream, Func<nint, Stream> openHandle, Func<int, bool> isInheritedDescriptor)
    {
        if (runsOnWindows)
        {
            return openConsoleStream();
        }

        return isInheritedDescriptor(UnixStandardErrorFileDescriptor) ? openHandle(UnixStandardErrorFileDescriptor) : Stream.Null;
    }
}
