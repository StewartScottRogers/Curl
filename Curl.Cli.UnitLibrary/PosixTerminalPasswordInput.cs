using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Where curl 8.22.0's POSIX <c>getpass_r</c> reads a password from
/// (<see href="https://github.com/curl/curl/blob/master/src/tool_getpass.c"/>): <c>/dev/tty</c> when it
/// opens, else standard input, with echo turned off through <c>tcgetattr</c> and <c>tcsetattr</c> when
/// that descriptor is a terminal, and turned back on once the password is read (ADR-0775).
/// </summary>
/// <remarks>
/// The input is opened on the first key read and kept for the rest of the process, so a second
/// prompt (<c>--proxy-user</c> after <c>-u</c>) reads on from the same place. Only <c>c_lflag</c>'s
/// <c>ECHO</c> bit is changed; it sits at byte 12 of glibc's and the BSDs' <c>termios</c> and at
/// byte 24 of Darwin's, whose <c>tcflag_t</c> is a 64-bit <c>unsigned long</c>, and is <c>0x8</c> on
/// both. Every line needs the real calls, which Windows has not, so per ADR-0083 the type is left
/// out of the Windows coverage measure.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin /dev/tty, tcgetattr and tcsetattr adapter, used off Windows only.")]
internal static partial class PosixTerminalPasswordInput
{
    private const uint EchoFlag = 0x8;
    private const int SetNow = 0;
    private const int SetAfterFlush = 2;
    private const int StandardInputDescriptor = 0;
    private const int TerminalSettingsLength = 256;

    private static readonly Lazy<(TextReader Reader, int Descriptor)> Input = new(OpenTerminalOrStandardInput);

    private static byte[]? settingsWithEcho;

    private static int LocalModesOffset => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() ? 24 : 12;

    /// <summary>Reads the next character typed; throws <see cref="InvalidOperationException"/> at the end of the input.</summary>
    public static ConsoleKeyInfo ReadKey()
    {
        int character = Input.Value.Reader.Read();
        return character < 0
            ? throw new InvalidOperationException("The password input has ended.")
            : new ConsoleKeyInfo((char)character, default, shift: false, alt: false, control: false);
    }

    /// <summary>Turns echo off on the password input, as curl's <c>ttyecho(FALSE, fd)</c>; false when the input is not a terminal.</summary>
    public static bool TurnEchoOff()
    {
        int descriptor = Input.Value.Descriptor;
        byte[] settings = new byte[TerminalSettingsLength];
        if (GetTerminalSettings(descriptor, settings) != 0)
        {
            return false;
        }

        settingsWithEcho = (byte[])settings.Clone();
        Span<byte> localModes = settings.AsSpan(LocalModesOffset, sizeof(uint));
        MemoryMarshal.Write(localModes, MemoryMarshal.Read<uint>(localModes) & ~EchoFlag);
        _ = SetTerminalSettings(descriptor, SetNow, settings);
        return true;
    }

    /// <summary>Puts back the settings <see cref="TurnEchoOff"/> replaced, as curl's <c>ttyecho(TRUE, fd)</c>.</summary>
    public static void TurnEchoOn()
    {
        if (settingsWithEcho is { } settings)
        {
            _ = SetTerminalSettings(Input.Value.Descriptor, SetAfterFlush, settings);
        }
    }

    private static (TextReader Reader, int Descriptor) OpenTerminalOrStandardInput()
    {
        try
        {
            FileStream terminal = new("/dev/tty", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 0);
            return (new StreamReader(terminal, Encoding.UTF8, detectEncodingFromByteOrderMarks: false), (int)terminal.SafeFileHandle.DangerousGetHandle());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (new StreamReader(Console.OpenStandardInput(), Encoding.UTF8, detectEncodingFromByteOrderMarks: false), StandardInputDescriptor);
        }
    }

    [LibraryImport("libc", EntryPoint = "tcgetattr")]
    private static partial int GetTerminalSettings(int descriptor, [Out] byte[] settings);

    [LibraryImport("libc", EntryPoint = "tcsetattr")]
    private static partial int SetTerminalSettings(int descriptor, int when, byte[] settings);
}
