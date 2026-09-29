using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Finds the known-hosts file an <c>scp</c> or <c>sftp</c> transfer checks the server's host key
/// against when neither <c>-k</c> nor <c>--knownhosts</c> was given, as curl 8.21.0's tool does with
/// <c>findfile(".ssh/known_hosts", FALSE)</c> (<c>src/tool_findfile.c</c>, ADR-0122): the first file
/// that can be read of <c>.ssh/known_hosts</c> in <c>CURL_HOME</c>, <c>HOME</c>, then on Windows
/// <c>USERPROFILE</c>, <c>APPDATA</c> and <c>USERPROFILE</c> + <c>\Application Data</c>, then off
/// Windows the account's home directory from the user database (<c>getpwuid</c>).
/// </summary>
/// <remarks>
/// A variable that is unset or empty is skipped. <c>XDG_CONFIG_HOME</c> and the <c>/.config</c>
/// directories are <c>curlrc</c> directories and are never searched for this file. The directory
/// and <c>.ssh/known_hosts</c> are joined with <c>\</c> on Windows and <c>/</c> elsewhere, curl's
/// <c>DIR_CHAR</c>. Measured with the local curl 8.21.0 (mingw, Schannel) on 2026-09-29 by giving
/// each variable its own directory holding <c>.ssh/known_hosts</c> (BL-576 Notes).
/// </remarks>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null" /> when it is not set.</param>
/// <param name="runsOnWindows">Whether to search as curl's Windows build does.</param>
/// <param name="accountHomeDirectory">The account's home directory from the user database, searched last off Windows; <see langword="null" /> or empty when unknown.</param>
internal sealed class SshKnownHostsFileSearch(Func<string, string?> readEnvironmentVariable, bool runsOnWindows, string? accountHomeDirectory)
{
    /// <summary>The file curl looks for in each directory.</summary>
    internal const string RelativePath = ".ssh/known_hosts";

    /// <summary>The variable naming each directory, what curl appends to it, and whether only curl's Windows build looks there.</summary>
    private static readonly (string EnvironmentVariable, string Suffix, bool WindowsOnly)[] DirectoriesInSearchOrder =
    [
        ("CURL_HOME", string.Empty, false),
        ("HOME", string.Empty, false),
        ("USERPROFILE", string.Empty, true),
        ("APPDATA", string.Empty, true),
        ("USERPROFILE", "\\Application Data", true),
    ];

    /// <summary>Lists the paths curl tries, in the order it tries them.</summary>
    /// <returns>The paths; the first one that can be read is the file.</returns>
    internal IReadOnlyList<string> CandidatePaths()
    {
        string separator = runsOnWindows ? "\\" : "/";
        IEnumerable<string?> directories = DirectoriesInSearchOrder
            .Select(DirectoryOf)
            .Append(runsOnWindows ? null : accountHomeDirectory);
        return [.. directories.Where(directory => !string.IsNullOrEmpty(directory)).Select(directory => directory + separator + RelativePath)];
    }

    /// <summary>
    /// Gets one entry's directory: its variable's value with the entry's suffix, or <see langword="null" />
    /// when the variable is unset or empty, or the entry is Windows-only and the search is not.
    /// </summary>
    private string? DirectoryOf((string EnvironmentVariable, string Suffix, bool WindowsOnly) entry)
    {
        string? home = entry.WindowsOnly && !runsOnWindows ? null : readEnvironmentVariable(entry.EnvironmentVariable);
        return string.IsNullOrEmpty(home) ? null : home + entry.Suffix;
    }

    /// <summary>Finds the first candidate path <paramref name="reader" /> can read.</summary>
    /// <param name="reader">Reads the candidate files.</param>
    /// <returns>The known-hosts file, or <see langword="null" /> when none can be read.</returns>
    internal string? Find(IDataFileReader reader) =>
        CandidatePaths().FirstOrDefault(path => reader.TryReadFile(path, out _));
}
