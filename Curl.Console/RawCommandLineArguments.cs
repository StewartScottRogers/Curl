using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Gives <c>Main</c>'s arguments with every byte that is not valid UTF-8 kept (CGR-016, BL-3849, ADR-0821).
/// On Linux and macOS .NET decodes argv as UTF-8 and replaces each invalid byte with U+FFFD, where curl
/// uses argv's bytes unchanged; so off Windows the arguments are read again from the process's raw argv
/// (<c>/proc/self/cmdline</c> on Linux, <c>_NSGetArgv</c> on macOS) and decoded with
/// <see cref="InvalidUtf8ByteEscape.Decode"/>. Windows hands over UTF-16 arguments, so they stay as given.
/// </summary>
internal static partial class RawCommandLineArguments
{
    /// <summary>
    /// <paramref name="arguments"/> as the running platform's raw argv spells them, or
    /// <paramref name="arguments"/> itself on Windows or when argv cannot be read.
    /// </summary>
    /// <param name="arguments">The arguments .NET handed <c>Main</c>.</param>
    /// <returns>The arguments to parse.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0821: picks the platform's argv reader, which only that platform has.")]
    internal static IReadOnlyList<string> ForCurrentPlatform(string[] arguments) =>
        OperatingSystem.IsLinux() ? Select(arguments, ReadLinuxCommandLine())
        : OperatingSystem.IsMacOS() ? Select(arguments, ReadMacOSArgv())
        : arguments;

    /// <summary>
    /// Splits the contents of <c>/proc/self/cmdline</c>, where each argument, the program path first,
    /// ends with a NUL byte, into the arguments' bytes.
    /// </summary>
    /// <param name="commandLine">The file's bytes.</param>
    /// <returns>Each argument's bytes, the program path first.</returns>
    internal static IReadOnlyList<byte[]> SplitProcCommandLine(ReadOnlySpan<byte> commandLine)
    {
        List<byte[]> rawArguments = [];
        while (!commandLine.IsEmpty)
        {
            int end = commandLine.IndexOf((byte)0);
            int length = end < 0 ? commandLine.Length : end;
            rawArguments.Add(commandLine[..length].ToArray());
            commandLine = commandLine[Math.Min(length + 1, commandLine.Length)..];
        }

        return rawArguments;
    }

    /// <summary>
    /// <paramref name="arguments"/> rebuilt from the last <paramref name="arguments"/>.Count entries of
    /// <paramref name="rawArguments"/> (argv after the program path, and after <c>dotnet</c>'s own
    /// arguments when it hosts the program), each decoded with <see cref="InvalidUtf8ByteEscape.Decode"/>;
    /// <paramref name="arguments"/> unchanged when argv could not be read, holds too few entries, or holds
    /// valid UTF-8 that does not read as the argument .NET gave, since then the two do not line up.
    /// </summary>
    /// <param name="arguments">The arguments .NET handed <c>Main</c>.</param>
    /// <param name="rawArguments">argv's bytes, or <see langword="null"/> when they could not be read.</param>
    /// <returns>The arguments to parse.</returns>
    internal static IReadOnlyList<string> Select(IReadOnlyList<string> arguments, IReadOnlyList<byte[]>? rawArguments)
    {
        if (rawArguments is null || rawArguments.Count < arguments.Count)
        {
            return arguments;
        }

        int skipped = rawArguments.Count - arguments.Count;
        string[] selected = new string[arguments.Count];
        for (int index = 0; index < selected.Length; index++)
        {
            byte[] raw = rawArguments[skipped + index];
            if (System.Text.Unicode.Utf8.IsValid(raw) && Encoding.UTF8.GetString(raw) != arguments[index])
            {
                return arguments;
            }

            selected[index] = InvalidUtf8ByteEscape.Decode(raw);
        }

        return selected;
    }

    /// <summary>Reads <c>/proc/self/cmdline</c>.</summary>
    /// <returns>argv's bytes, or <see langword="null"/> when the file cannot be read.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0821: reads a file only Linux has.")]
    private static IReadOnlyList<byte[]>? ReadLinuxCommandLine()
    {
        try
        {
            return SplitProcCommandLine(File.ReadAllBytes("/proc/self/cmdline"));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads the process's argv through libSystem's <c>_NSGetArgc</c> and <c>_NSGetArgv</c>.</summary>
    /// <returns>argv's bytes.</returns>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-0821: a thin libSystem adapter only macOS has.")]
    private static unsafe IReadOnlyList<byte[]> ReadMacOSArgv()
    {
        int count = *GetArgc();
        byte** argv = *GetArgv();
        byte[][] rawArguments = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            rawArguments[index] = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(argv[index]).ToArray();
        }

        return rawArguments;
    }

    /// <summary>libSystem's <c>_NSGetArgc</c>: the address of the process's argc.</summary>
    /// <returns>The address.</returns>
    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_NSGetArgc")]
    private static unsafe partial int* GetArgc();

    /// <summary>libSystem's <c>_NSGetArgv</c>: the address of the process's argv.</summary>
    /// <returns>The address.</returns>
    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_NSGetArgv")]
    private static unsafe partial byte*** GetArgv();
}
