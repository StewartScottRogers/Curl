using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Curl.Cli;

/// <summary>
/// Where curl 8.22.0's Windows <c>getpass_r</c> reads a password from
/// (<see href="https://github.com/curl/curl/blob/master/src/tool_getpass.c"/>): the CRT's <c>_getch</c>,
/// which reads the console input buffer (<c>CONIN$</c>) one character at a time with every input mode
/// turned off, so nothing is echoed and standard input, redirected or not, is never read (ADR-0828).
/// </summary>
/// <remarks>
/// <c>CONIN$</c> is opened on the first key read and kept for the rest of the process, so a second
/// prompt (<c>--proxy-user</c> after <c>-u</c>) reads on from the same console. Every line needs a
/// real console, which a test run has not, so per ADR-0083 the type is left out of the coverage measure.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin CONIN$, SetConsoleMode and ReadConsole adapter that needs a real console.")]
public static partial class WindowsConsolePasswordInput
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x3;
    private const uint OpenExisting = 3;
    private const uint NoInputModes = 0;

    private static readonly Lazy<SafeFileHandle> ConsoleInput = new(() => CreateFileW("CONIN$", GenericRead | GenericWrite, FileShareReadWrite, 0, OpenExisting, 0, 0));

    /// <summary>
    /// Reads the next character typed at the console, as <c>_getch</c>: with every input mode off for
    /// the read and the console's modes put back after it. Throws <see cref="InvalidOperationException"/>
    /// when the process has no console to read.
    /// </summary>
    /// <returns>The character typed.</returns>
    public static ConsoleKeyInfo ReadKey()
    {
        SafeFileHandle input = ConsoleInput.Value;
        if (input.IsInvalid || !GetConsoleMode(input, out uint modes))
        {
            throw new InvalidOperationException("The process has no console to read the password from.");
        }

        _ = SetConsoleMode(input, NoInputModes);
        try
        {
            return ReadConsoleW(input, out ushort character, 1, out uint read, 0) && read == 1
                ? new ConsoleKeyInfo((char)character, default, shift: false, alt: false, control: false)
                : throw new InvalidOperationException("The console input could not be read.");
        }
        finally
        {
            _ = SetConsoleMode(input, modes);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, nint securityAttributes, uint creationDisposition, uint flagsAndAttributes, nint templateFile);

    [LibraryImport("kernel32.dll", EntryPoint = "GetConsoleMode")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(SafeFileHandle consoleHandle, out uint modes);

    [LibraryImport("kernel32.dll", EntryPoint = "SetConsoleMode")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(SafeFileHandle consoleHandle, uint modes);

    [LibraryImport("kernel32.dll", EntryPoint = "ReadConsoleW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ReadConsoleW(SafeFileHandle consoleInput, out ushort buffer, uint charactersToRead, out uint charactersRead, nint inputControl);
}
