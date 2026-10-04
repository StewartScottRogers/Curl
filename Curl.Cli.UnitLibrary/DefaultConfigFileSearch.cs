namespace Curl.Cli;

/// <summary>
/// Lists the paths where curl 8.21.0 looks for its default config file (<c>.curlrc</c>), in the
/// order it tries them, ported from <c>findfile</c> (<c>src/tool_findfile.c</c>) and
/// <c>open_config_file</c> (<c>src/tool_parsecfg.c</c>). The environment, the executable's directory
/// and the account's home directory are injected, so the order is tested without touching the
/// process; the first path that can be read is the file (see
/// <see cref="CommandLineParser.Parse(IReadOnlyList{string}, Func{string, bool}, IPasswordPrompt, IDataFileReader, DefaultConfigFileSearch)"/>).
/// </summary>
/// <remarks>
/// <para>
/// Each variable below is skipped when it is unset or empty; the rest are tried in this order, a
/// directory and its file name joined with <c>\</c> on Windows and <c>/</c> elsewhere:
/// <c>CURL_HOME</c>, <c>XDG_CONFIG_HOME</c> (file <c>curlrc</c>, no dot), <c>HOME</c>, then on Windows
/// <c>USERPROFILE</c>, <c>APPDATA</c> and <c>USERPROFILE</c> + <c>\Application Data</c>, then
/// <c>CURL_HOME</c> + <c>/.config</c> and <c>HOME</c> + <c>/.config</c> (file <c>curlrc</c>). On Windows
/// each directory not given as a <c>curlrc</c> one is tried for <c>.curlrc</c> and then <c>_curlrc</c>;
/// elsewhere for <c>.curlrc</c> alone. After them come the account's home directory from the user
/// database (<c>getpwuid</c>) for <c>.curlrc</c> everywhere but Windows, and the executable's
/// directory for <c>.curlrc</c> and then <c>_curlrc</c> on Windows alone.
/// </para>
/// <para>
/// curl's one quirk is kept: the first <c>curlrc</c> directory it reaches (<c>XDG_CONFIG_HOME</c>, or
/// else <c>CURL_HOME/.config</c>) is tried whether or not the file is there, and from then on only
/// <c>.curlrc</c> is tried and no other <c>curlrc</c> directory is. So with <c>XDG_CONFIG_HOME</c> set,
/// <c>HOME\_curlrc</c> and <c>HOME/.config/curlrc</c> are never read, and with <c>CURL_HOME</c> set,
/// <c>HOME/.config/curlrc</c> is never read.
/// </para>
/// <para>
/// Measured with the local curl 8.21.0 (mingw, Schannel) on 2026-09-27 by giving each directory a
/// <c>.curlrc</c> holding one unknown option and reading the path back from curl's error line; the
/// executable's directory with a copy of <c>curl.exe</c>. The Linux and macOS order follows the
/// source and the manual (<see href="https://curl.se/docs/manpage.html#-K"/>).
/// </para>
/// </remarks>
/// <param name="readEnvironmentVariable">Reads an environment variable; <see langword="null"/> when it is not set.</param>
/// <param name="isWindows">Whether to search as curl's Windows build does.</param>
/// <param name="executableDirectory">The directory of the running executable, searched last on Windows; <see langword="null"/> when unknown.</param>
/// <param name="accountHomeDirectory">The account's home directory from the user database, searched last elsewhere; <see langword="null"/> or empty when unknown.</param>
public sealed class DefaultConfigFileSearch(Func<string, string?> readEnvironmentVariable, bool isWindows, string? executableDirectory, string? accountHomeDirectory)
{
    private const string DottedName = ".curlrc";

    private const string UnderscoredName = "_curlrc";

    private const string ConfigDirectoryName = "curlrc";

    /// <summary>curl's <c>conf_list</c>: the variable naming each directory, what curl appends to it, whether the file there is <c>curlrc</c> with no dot, and whether only curl's Windows build looks there.</summary>
    private static readonly (string EnvironmentVariable, string Suffix, bool HoldsConfigDirectoryName, bool WindowsOnly)[] DirectoriesInSearchOrder =
    [
        ("CURL_HOME", string.Empty, false, false),
        ("XDG_CONFIG_HOME", string.Empty, true, false),
        ("HOME", string.Empty, false, false),
        ("USERPROFILE", string.Empty, false, true),
        ("APPDATA", string.Empty, false, true),
        ("USERPROFILE", "\\Application Data", false, true),
        ("CURL_HOME", "/.config", true, false),
        ("HOME", "/.config", true, false),
    ];

    /// <summary>
    /// The search this process makes: its environment variables, the directory of
    /// <see cref="Environment.ProcessPath"/>, the account home directory off Windows (<see cref="AccountHomeDirectory"/>), and
    /// Windows' order when running on Windows.
    /// </summary>
    public static DefaultConfigFileSearch ForProcess { get; } = new(
        Environment.GetEnvironmentVariable,
        OperatingSystem.IsWindows(),
        Path.GetDirectoryName(Environment.ProcessPath),
        AccountHomeDirectory.ForProcess);

    /// <summary>Lists the paths curl tries for its default config file, in the order it tries them.</summary>
    /// <returns>The paths; the first one that can be read is the file.</returns>
    public IReadOnlyList<string> CandidatePaths()
    {
        string separator = isWindows ? "\\" : "/";
        SearchState state = new() { TriesUnderscore = isWindows, TriesConfigDirectory = true };
        List<string> paths = [];
        foreach ((string EnvironmentVariable, string Suffix, bool HoldsConfigDirectoryName, bool WindowsOnly) directory in DirectoriesInSearchOrder)
        {
            string? home = directory.WindowsOnly && !isWindows ? null : readEnvironmentVariable(directory.EnvironmentVariable);
            if (!string.IsNullOrEmpty(home))
            {
                AddDirectory(paths, home + directory.Suffix + separator, directory.HoldsConfigDirectoryName, state);
            }
        }

        AddLastDirectory(paths, separator);
        return paths;
    }

    /// <summary>
    /// Adds the paths curl tries in one directory, <paramref name="prefix"/> ending in its separator: its
    /// <c>curlrc</c> when it <paramref name="holdsConfigDirectoryName"/> and no such directory has been
    /// reached yet, which ends the <c>_curlrc</c> and <c>curlrc</c> checks for good; otherwise
    /// <c>.curlrc</c>, and <c>_curlrc</c> while <paramref name="state"/> still tries it.
    /// </summary>
    private static void AddDirectory(List<string> paths, string prefix, bool holdsConfigDirectoryName, SearchState state)
    {
        if (!holdsConfigDirectoryName)
        {
            AddDottedAndUnderscored(paths, prefix, state.TriesUnderscore);
        }
        else if (state.TriesConfigDirectory)
        {
            paths.Add(prefix + ConfigDirectoryName);
            state.TriesConfigDirectory = false;
            state.TriesUnderscore = false;
        }
    }

    private void AddLastDirectory(List<string> paths, string separator)
    {
        string? lastDirectory = isWindows ? executableDirectory : accountHomeDirectory;
        if (!string.IsNullOrEmpty(lastDirectory))
        {
            AddDottedAndUnderscored(paths, lastDirectory + separator, isWindows);
        }
    }

    private static void AddDottedAndUnderscored(List<string> paths, string prefix, bool triesUnderscore)
    {
        paths.Add(prefix + DottedName);
        if (triesUnderscore)
        {
            paths.Add(prefix + UnderscoredName);
        }
    }

    /// <summary>curl's <c>dotscore</c>, split in two: what the search still tries.</summary>
    private sealed class SearchState
    {
        public bool TriesUnderscore { get; set; }

        public bool TriesConfigDirectory { get; set; }
    }
}
