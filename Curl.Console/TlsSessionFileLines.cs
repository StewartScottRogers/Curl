using Curl.Cli;
using Curl.Networking;

namespace Curl.Console;

/// <summary>
/// Reads the <c>--ssl-sessions</c> file into the run's <see cref="TlsSessionCache" /> before the
/// transfers and writes it back after them, as curl 8.21.0's <c>src/tool_ssls.c</c> does
/// (ADR-0319), returning the lines curl writes to standard error for each.
/// </summary>
internal static class TlsSessionFileLines
{
    /// <summary>
    /// Loads the file <see cref="CommandLineOptions.SslSessionsFile" /> names. A file that does
    /// not exist is no error: under <c>-v</c> or a trace curl notes
    /// <c>Note: SSL session file does not exist (yet?): F</c>. Otherwise each line the cache
    /// refuses gives its warning, unless <c>-s</c>.
    /// </summary>
    /// <param name="sessions">The run's cache.</param>
    /// <param name="options">The run's options.</param>
    /// <returns>The lines to write to standard error.</returns>
    internal static IReadOnlyList<string> Load(TlsSessionCache sessions, CommandLineOptions options)
    {
        if (options.SslSessionsFile is not { } file)
        {
            return [];
        }

        if (!File.Exists(file))
        {
            return options.Trace == TraceKind.None ? [] : [$"Note: SSL session file does not exist (yet?): {file}"];
        }

        IReadOnlyList<string> warnings = sessions.Import(File.ReadAllText(file), file);
        return options.Silent ? [] : warnings;
    }

    /// <summary>
    /// Saves the cache to the file, replacing what it held, with CRLF line ends on Windows as
    /// curl's text-mode write gives them. A file that cannot be created gives
    /// <c>Warning: Failed to create SSL session file F</c>, unless <c>-s</c>, and changes no exit code.
    /// </summary>
    /// <param name="sessions">The run's cache.</param>
    /// <param name="options">The run's options.</param>
    /// <param name="runsOnWindows">Whether the lines end in CRLF.</param>
    /// <returns>The lines to write to standard error.</returns>
    internal static IReadOnlyList<string> Save(TlsSessionCache sessions, CommandLineOptions options, bool runsOnWindows)
    {
        if (options.SslSessionsFile is not { } file)
        {
            return [];
        }

        string text = sessions.Export();
        try
        {
            File.WriteAllText(file, runsOnWindows ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text);
            return [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return options.Silent ? [] : [$"Warning: Failed to create SSL session file {file}"];
        }
    }
}
