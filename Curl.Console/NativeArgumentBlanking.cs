using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Curl.Cli;
using Microsoft.Win32.SafeHandles;

namespace Curl.Console;

/// <summary>
/// Overwrites the secret values on the process's own argv with asterisks of the same byte length, as curl's
/// <c>cleanarg()</c> does (STD-048, BL-5093, ADR-1265), so <c>ps</c> and <c>/proc/PID/cmdline</c> do not show
/// them: the values of <c>-u</c>/<c>--user</c>, <c>-U</c>/<c>--proxy-user</c>, <c>--oauth2-bearer</c>,
/// <c>--pass</c>, <c>--proxy-pass</c>, <c>-E</c>/<c>--cert</c> and <c>--proxy-cert</c>. The arguments the run
/// parses are managed copies read before the blanking, so the credentials are still used. Linux writes argv
/// through <c>/proc/self/mem</c>, macOS through <c>_NSGetArgv</c>; Windows argv is not shown to other users
/// the same way and curl's Windows build does not blank it, so nothing is done there.
/// </summary>
internal static partial class NativeArgumentBlanking
{
    /// <summary>The long names of the options whose value curl blanks.</summary>
    private static readonly HashSet<string> SecretLongNames =
        new(["user", "proxy-user", "oauth2-bearer", "pass", "proxy-pass", "cert", "proxy-cert"], StringComparer.Ordinal);

    /// <summary>
    /// The secret values among <paramref name="arguments"/>: for each, the argument's index and the offset of
    /// the value inside it (0 for a separate value, past <c>-u</c> or <c>--user=</c> for an attached one).
    /// The offset counts ASCII option characters only, so it is the same in bytes as in characters.
    /// Scanning stops at <c>--</c>, after which every argument is a URL.
    /// </summary>
    /// <param name="arguments">The arguments after the program path.</param>
    /// <returns>Each secret value's argument index and start offset, in argument order.</returns>
    internal static IReadOnlyList<(int Index, int Start)> SecretValues(IReadOnlyList<string> arguments)
    {
        List<(int Index, int Start)> secrets = [];
        int index = 0;
        while (index < arguments.Count && arguments[index] != "--")
        {
            string argument = arguments[index];
            index += argument.StartsWith("--", StringComparison.Ordinal)
                ? ScanLong(arguments, index, secrets)
                : argument.Length > 1 && argument[0] == '-' ? ScanShortBundle(arguments, index, secrets) : 1;
        }

        return secrets;
    }

    /// <summary>
    /// The byte ranges inside <paramref name="commandLine"/> (argv's NUL-terminated strings, the program path
    /// first, as <c>/proc/self/cmdline</c> holds them) that hold the secret values of <paramref name="arguments"/>,
    /// which are the last <paramref name="arguments"/>.Count strings; none when argv holds fewer.
    /// </summary>
    /// <param name="commandLine">argv's bytes.</param>
    /// <param name="arguments">The arguments after the program path.</param>
    /// <returns>Each secret value's offset and byte length inside <paramref name="commandLine"/>.</returns>
    internal static IReadOnlyList<(int Offset, int Length)> SecretRanges(ReadOnlySpan<byte> commandLine, IReadOnlyList<string> arguments)
    {
        List<int> starts = [];
        List<int> lengths = [];
        int position = 0;
        while (position < commandLine.Length)
        {
            int end = commandLine[position..].IndexOf((byte)0);
            int length = end < 0 ? commandLine.Length - position : end;
            starts.Add(position);
            lengths.Add(length);
            position += length + 1;
        }

        List<(int Offset, int Length)> ranges = [];
        int skipped = starts.Count - arguments.Count;
        if (skipped < 0)
        {
            return ranges;
        }

        foreach ((int index, int start) in SecretValues(arguments))
        {
            int rawLength = lengths[skipped + index];
            if (rawLength > start)
            {
                ranges.Add((starts[skipped + index] + start, rawLength - start));
            }
        }

        return ranges;
    }

    /// <summary>Overwrites each of <paramref name="ranges"/> inside <paramref name="commandLine"/> with asterisks.</summary>
    /// <param name="commandLine">argv's bytes.</param>
    /// <param name="ranges">The ranges <see cref="SecretRanges"/> gave.</param>
    internal static void Blank(Span<byte> commandLine, IReadOnlyList<(int Offset, int Length)> ranges)
    {
        foreach ((int offset, int length) in ranges)
        {
            commandLine.Slice(offset, length).Fill((byte)'*');
        }
    }

    /// <summary>
    /// The start and end addresses of argv's strings, fields 48 and 49 (<c>arg_start</c>, <c>arg_end</c>) of
    /// <c>/proc/self/stat</c>; <see langword="null"/> when the line holds fewer fields.
    /// </summary>
    /// <param name="statLine">The file's text.</param>
    /// <returns>The two addresses.</returns>
    internal static (long Start, long End)? ArgumentAddresses(string statLine)
    {
        // Field 2, the command name in parentheses, may hold spaces; the fields after it start at field 3.
        string[] fields = statLine[(statLine.LastIndexOf(')') + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        const int ArgumentStartField = 48 - 3;
        return fields.Length > ArgumentStartField + 1
            ? (long.Parse(fields[ArgumentStartField], CultureInfo.InvariantCulture), long.Parse(fields[ArgumentStartField + 1], CultureInfo.InvariantCulture))
            : null;
    }

    /// <summary>Blanks the secret values on the running process's argv; nothing on Windows.</summary>
    /// <param name="arguments">The arguments after the program path, as already read.</param>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-1265: picks the platform's argv writer, which only that platform has.")]
    internal static void BlankForCurrentPlatform(IReadOnlyList<string> arguments)
    {
        if (OperatingSystem.IsLinux())
        {
            BlankLinux(arguments);
        }
        else if (OperatingSystem.IsMacOS())
        {
            BlankMacOS(arguments);
        }
    }

    /// <summary>Rewrites argv's secret values through <c>/proc/self/mem</c>; leaves argv as it is when it cannot.</summary>
    /// <param name="arguments">The arguments after the program path.</param>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-1265: writes process memory through a file only Linux has.")]
    private static void BlankLinux(IReadOnlyList<string> arguments)
    {
        try
        {
            if (ArgumentAddresses(File.ReadAllText("/proc/self/stat")) is not (long start, long end) || end <= start)
            {
                return;
            }

            using SafeFileHandle memory = File.OpenHandle("/proc/self/mem", FileMode.Open, FileAccess.ReadWrite);
            byte[] commandLine = new byte[end - start];
            int read = RandomAccess.Read(memory, commandLine, start);
            IReadOnlyList<(int Offset, int Length)> ranges = SecretRanges(commandLine.AsSpan(0, read), arguments);
            Blank(commandLine, ranges);
            foreach ((int offset, int length) in ranges)
            {
                RandomAccess.Write(memory, commandLine.AsSpan(offset, length), start + offset);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Rewrites argv's secret values through libSystem's <c>_NSGetArgc</c> and <c>_NSGetArgv</c>.</summary>
    /// <param name="arguments">The arguments after the program path.</param>
    [ExcludeFromCodeCoverage(Justification = "ADR-0083, ADR-1265: a thin libSystem adapter only macOS has.")]
    private static unsafe void BlankMacOS(IReadOnlyList<string> arguments)
    {
        int skipped = *GetArgc() - arguments.Count;
        if (skipped < 0)
        {
            return;
        }

        byte** argv = *GetArgv();
        foreach ((int index, int start) in SecretValues(arguments))
        {
            Span<byte> argument = MemoryMarshal.CreateSpan(ref *argv[skipped + index], int.MaxValue);
            int length = argument.IndexOf((byte)0);
            if (length > start)
            {
                argument[start..length].Fill((byte)'*');
            }
        }
    }

    /// <summary>libSystem's <c>_NSGetArgc</c>: the address of the process's argc.</summary>
    /// <returns>The address.</returns>
    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_NSGetArgc")]
    private static unsafe partial int* GetArgc();

    /// <summary>libSystem's <c>_NSGetArgv</c>: the address of the process's argv.</summary>
    /// <returns>The address.</returns>
    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_NSGetArgv")]
    private static unsafe partial byte*** GetArgv();

    /// <summary>Scans one <c>--name</c> or <c>--name=value</c> argument.</summary>
    /// <returns>How many arguments the option used.</returns>
    private static int ScanLong(IReadOnlyList<string> arguments, int index, List<(int Index, int Start)> secrets)
    {
        string argument = arguments[index];
        int equals = argument.IndexOf('=', StringComparison.Ordinal);
        string longName = equals >= 0 ? argument[2..equals] : argument[2..];
        CommandLineOption? option = FindOption(row => row.LongName == longName);
        return equals >= 0 ? Attached(option, index, equals + 1, secrets) : Separate(option, arguments, index, secrets);
    }

    /// <summary>Scans one <c>-abc</c> bundle of short letters, ending at the first letter that takes a value.</summary>
    /// <returns>How many arguments the bundle used.</returns>
    private static int ScanShortBundle(IReadOnlyList<string> arguments, int index, List<(int Index, int Start)> secrets)
    {
        string argument = arguments[index];
        for (int letter = 1; letter < argument.Length; letter++)
        {
            char shortName = argument[letter];
            CommandLineOption? option = FindOption(row => row.ShortName == shortName);
            if (option is null || option.EndsBundle)
            {
                return 1;
            }

            if (TakesValue(option))
            {
                return letter + 1 < argument.Length ? Attached(option, index, letter + 1, secrets) : Separate(option, arguments, index, secrets);
            }
        }

        return 1;
    }

    /// <summary>Records a value attached to its option inside the argument at <paramref name="index"/>.</summary>
    /// <returns>1, the one argument used.</returns>
    private static int Attached(CommandLineOption? option, int index, int start, List<(int Index, int Start)> secrets)
    {
        if (IsSecret(option))
        {
            secrets.Add((index, start));
        }

        return 1;
    }

    /// <summary>Records the argument after <paramref name="index"/> as the option's value when it takes one.</summary>
    /// <returns>How many arguments the option used: 2 with a value, else 1.</returns>
    private static int Separate(CommandLineOption? option, IReadOnlyList<string> arguments, int index, List<(int Index, int Start)> secrets)
    {
        if (option is null || !TakesValue(option) || index + 1 >= arguments.Count)
        {
            return 1;
        }

        if (IsSecret(option))
        {
            secrets.Add((index + 1, 0));
        }

        return 2;
    }

    private static bool TakesValue(CommandLineOption option) => option.TakesValue || option.TakesSubject;

    private static bool IsSecret(CommandLineOption? option) => option is not null && SecretLongNames.Contains(option.LongName);

    private static CommandLineOption? FindOption(Func<CommandLineOption, bool> matches)
    {
        foreach (CommandLineOption row in CommandLineOptionTable.Rows)
        {
            if (matches(row))
            {
                return row;
            }
        }

        return null;
    }
}
