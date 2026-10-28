using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Curl.Console;

/// <summary>
/// Reads the width of the terminal standard input is attached to, as curl's
/// <c>get_terminal_columns</c> does off Windows with <c>ioctl(STDIN_FILENO, TIOCGWINSZ)</c>,
/// through the .NET runtime's own shim (<c>SystemNative_GetWindowSize</c> in
/// <c>libSystem.Native</c>) (ADR-0864).
/// </summary>
/// <remarks>
/// <see cref="System.Console.WindowWidth" /> is not used off Windows: reading it initialises
/// .NET's terminfo console, which writes <c>keypad_xmit</c> (<c>ESC[?1h ESC=</c>) to the
/// terminal and never resets it (CGR-115). libc's <c>ioctl</c> is variadic, which a plain
/// import cannot call on macOS arm64, so the runtime's fixed-signature shim is used. Every line
/// needs the real call, which Windows has not, so per ADR-0083 the type is left out of the
/// Windows coverage measure.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0864: a thin TIOCGWINSZ adapter, used off Windows.")]
internal static partial class NativeTerminalWindowSize
{
    /// <summary>The file descriptor of standard input on Linux and macOS.</summary>
    private const nint StandardInputFileDescriptor = 0;

    /// <summary>
    /// Reads the column count of the terminal standard input is attached to.
    /// </summary>
    /// <returns>The column count, or <see langword="null" /> when standard input is not a terminal.</returns>
    internal static int? ReadStandardInputColumns() =>
        GetWindowSize(StandardInputFileDescriptor, out WindowSize size) == 0 ? size.Columns : null;

    /// <summary>The runtime's <c>SystemNative_GetWindowSize</c>, <c>ioctl(fd, TIOCGWINSZ)</c>.</summary>
    /// <param name="fileDescriptor">The file descriptor.</param>
    /// <param name="size">The terminal's size.</param>
    /// <returns>0 when the call succeeded, otherwise -1.</returns>
    [LibraryImport("libSystem.Native", EntryPoint = "SystemNative_GetWindowSize", SetLastError = true)]
    private static partial int GetWindowSize(nint fileDescriptor, out WindowSize size);

    /// <summary>The runtime's <c>WinSize</c> record, POSIX <c>struct winsize</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowSize
    {
        public ushort Rows;

        public ushort Columns;

        public ushort PixelWidth;

        public ushort PixelHeight;
    }
}
