using System.Runtime.InteropServices;

namespace Curl.Console;

/// <summary>
/// Turns on virtual-terminal processing for a Windows console on standard output, so the
/// escape sequences of styled header output (ADR-0246) show as bold rather than as text, and
/// puts the console's mode back when disposed, as curl 8.21.0's <c>tool_doswin.c</c> does.
/// </summary>
/// <param name="readMode">Reads the console mode of standard output, or <see langword="null" /> when it is no console.</param>
/// <param name="writeMode">Sets the console mode of standard output; <see langword="false" /> when that fails.</param>
internal sealed class StandardOutputVirtualTerminal(Func<uint?> readMode, Func<uint, bool> writeMode) : IDisposable
{
    /// <summary><c>ENABLE_VIRTUAL_TERMINAL_PROCESSING</c>, the console-mode flag for escape sequences.</summary>
    internal const uint EnableVirtualTerminalProcessing = 0x0004;

    /// <summary>The mode to put back on dispose, or <see langword="null" /> when nothing was changed.</summary>
    private uint? modeToRestore;

    /// <summary>
    /// Creates the one for this process's Windows console.
    /// </summary>
    /// <returns>One that reads and sets the console mode through <c>kernel32</c>.</returns>
    internal static StandardOutputVirtualTerminal ForWindowsConsole() => new(ReadConsoleMode, WriteConsoleMode);

    /// <summary>
    /// Tells whether styled header output renders on standard output, curl's
    /// <c>tool_term_has_bold</c> on a terminal: never when standard output is no terminal; always
    /// off Windows; on Windows once <see cref="Enable" /> succeeds.
    /// </summary>
    /// <param name="standardOutputIsTerminal">Whether standard output is a terminal.</param>
    /// <param name="isWindows">Whether the process runs on Windows.</param>
    /// <param name="enable">Turns virtual-terminal processing on; see <see cref="Enable" />.</param>
    /// <returns><see langword="true" /> when the terminal renders bold.</returns>
    internal static bool RendersStyles(bool standardOutputIsTerminal, bool isWindows, Func<bool> enable) =>
        standardOutputIsTerminal && (!isWindows || enable());

    /// <summary>
    /// Turns on virtual-terminal processing, remembering the mode to put back.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when it was already on or is now; <see langword="false" /> when
    /// standard output is no console or its mode cannot be set.
    /// </returns>
    internal bool Enable()
    {
        if (readMode() is not { } mode)
        {
            return false;
        }

        if ((mode & EnableVirtualTerminalProcessing) != 0)
        {
            return true;
        }

        if (!writeMode(mode | EnableVirtualTerminalProcessing))
        {
            return false;
        }

        modeToRestore = mode;
        return true;
    }

    /// <summary>Puts back the console mode <see cref="Enable" /> changed, if it changed one.</summary>
    public void Dispose()
    {
        if (modeToRestore is { } mode)
        {
            writeMode(mode);
            modeToRestore = null;
        }
    }

    /// <summary>Calls <c>GetConsoleMode</c> on standard output.</summary>
    /// <returns>The mode, or <see langword="null" /> when standard output is no console.</returns>
    internal static uint? ReadConsoleMode() =>
        ModeIfRead(GetConsoleMode(GetStdHandle(StandardOutputOpener.WindowsStandardOutputHandleId), out uint mode), mode);

    /// <summary>Gives the mode <c>GetConsoleMode</c> read, or <see langword="null" /> when it failed.</summary>
    /// <param name="read">What <c>GetConsoleMode</c> returned.</param>
    /// <param name="mode">The mode it wrote.</param>
    /// <returns><paramref name="mode" />, or <see langword="null" /> when <paramref name="read" /> is <see langword="false" />.</returns>
    internal static uint? ModeIfRead(bool read, uint mode) => read ? mode : null;

    /// <summary>Calls <c>SetConsoleMode</c> on standard output.</summary>
    /// <param name="mode">The mode to set.</param>
    /// <returns>Whether it was set.</returns>
    internal static bool WriteConsoleMode(uint mode) =>
        SetConsoleMode(GetStdHandle(StandardOutputOpener.WindowsStandardOutputHandleId), mode);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetStdHandle(int standardHandleId);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(nint consoleHandle, out uint mode);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(nint consoleHandle, uint mode);
}
