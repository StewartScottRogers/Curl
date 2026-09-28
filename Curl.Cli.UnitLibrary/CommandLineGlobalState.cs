namespace Curl.Cli;

/// <summary>
/// What every option group of one command line shares: the settings of the options
/// <see cref="CommandLineOptionTable.GlobalOptionLongNames"/> lists, which curl 8.21.0 keeps once for
/// the whole command line however many <c>-:</c> / <c>--next</c> groups there are, and the parse's own
/// bookkeeping (warning lines, open <c>-K</c> files, the groups themselves). Each
/// <see cref="CommandLineOptions"/> of the command line holds the same instance, so a global option
/// read in any group is in effect in all of them.
/// </summary>
internal sealed class CommandLineGlobalState
{
    /// <summary>The option groups in command-line order; the last is the one being filled in.</summary>
    public List<CommandLineOptions> Groups { get; } = [];

    public List<string> WarningLines { get; } = [];

    public List<StandardErrorRedirect> StandardErrorRedirects { get; } = [];

    public List<string?> ConfigFileHelpSubjects { get; } = [];

    public Dictionary<string, byte[]> Variables { get; } = new(StringComparer.Ordinal);

    public bool VersionRequested { get; set; }

    public bool HelpRequested { get; set; }

    public string? HelpSubject { get; set; }

    public bool ManualRequested { get; set; }

    public bool Silent { get; set; }

    public bool ShowError { get; set; }

    public bool ProgressMeterOff { get; set; }

    public bool ProgressBar { get; set; }

    public TraceKind Trace { get; set; }

    public string? TraceFile { get; set; }

    public int Verbosity { get; set; }

    public bool TraceTime { get; set; }

    public string? StandardErrorFile { get; set; }

    public bool StyledOutput { get; set; } = true;

    public bool FailEarly { get; set; }

    public bool Parallel { get; set; }

    public bool ParallelImmediate { get; set; }

    public int ParallelMax { get; set; } = CommandLineOptions.DefaultParallelMax;

    public int ParallelMaxHost { get; set; }

    public string? DefaultConfigFile { get; set; }

    public int OpenConfigFileCount { get; set; }

    public bool FirstOptionOfArgument { get; set; }

    public bool ReadingConfigFile { get; set; }
}
