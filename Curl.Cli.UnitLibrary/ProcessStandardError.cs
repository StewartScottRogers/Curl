using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32.SafeHandles;

namespace Curl.Cli;

/// <summary>
/// The process's standard error for the password prompt. Off Windows it is file descriptor 2 written
/// directly, because the first use of <see cref="Console.Error"/> makes .NET's terminfo setup write
/// <c>keypad_xmit</c> (<c>ESC [ ? 1 h ESC =</c>) to the terminal and never reset it, which upstream
/// curl does not (BL-5109); on Windows it is <see cref="Console.Error"/>.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "ADR-0083: a thin adapter over the process's real standard error.")]
internal static class ProcessStandardError
{
    private const int StandardErrorDescriptor = 2;

    /// <summary>Opens the writer; nothing is written until the prompt writes.</summary>
    public static TextWriter Open() =>
        OperatingSystem.IsWindows()
            ? Console.Error
            : new StreamWriter(new FileStream(new SafeFileHandle(StandardErrorDescriptor, ownsHandle: false), FileAccess.Write, bufferSize: 1), new System.Text.UTF8Encoding(false)) { AutoFlush = true };
}
