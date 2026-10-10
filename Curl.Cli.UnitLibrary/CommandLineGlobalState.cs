using Curl.Protocol.Abstractions;

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

    public bool AiHelpRequested { get; set; }

    public string? AiHelpSubject { get; set; }

    public bool EngineListRequested { get; set; }

    public bool CaEmbedDumpRequested { get; set; }

    public string? SslSessionsFile { get; set; }

    public string? LibcurlFile { get; set; }

    public bool ReadsArgumentsAsUtf8 { get; set; }

    public bool ActsAsWindowsSchannelBuild { get; set; }

    public Func<string, string?> ReadEnvironmentVariable { get; set; } = Environment.GetEnvironmentVariable;

    public bool Silent { get; set; }

    public bool ShowError { get; set; }

    public bool ProgressMeterOff { get; set; }

    public bool ProgressBar { get; set; }

    public TraceKind Trace { get; set; }

    public string? TraceFile { get; set; }

    public int Verbosity { get; set; }

    public bool TraceTime { get; set; }

    public bool TraceIds { get; set; }

    /// <summary><see langword="true"/> while <c>--trace-config ids</c> (or <c>all</c>) is in effect, which a first <c>-v</c> does not clear.</summary>
    public bool TraceConfigIds { get; set; }

    /// <summary><see langword="true"/> while <c>--trace-config time</c> (or <c>all</c>) is in effect, which a first <c>-v</c> does not clear.</summary>
    public bool TraceConfigTime { get; set; }

    /// <summary>The trace component names <c>--trace-config</c> turned on, lower case; <c>all</c> among them for every component.</summary>
    public HashSet<string> TraceComponents { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The names in <see cref="TraceComponents"/> that <c>-vv</c>, <c>-vvv</c> or <c>-vvvv</c> put there and
    /// no <c>--trace-config</c> has named since, which a first <c>-v</c> takes out again.
    /// </summary>
    public HashSet<string> VerbosityTraceComponents { get; } = new(StringComparer.Ordinal);

    public string? StandardErrorFile { get; set; }

    /// <summary>The level the last <c>--log-level</c> named; <see langword="null"/> when none was given.</summary>
    public DiagnosticLogLevel? DiagnosticLogLevelGiven { get; set; }

    public string? DiagnosticLogFile { get; set; }

    public bool StyledOutput { get; set; } = true;

    public bool FailEarly { get; set; }

    public bool Parallel { get; set; }

    public bool ParallelImmediate { get; set; }

    public int ParallelMax { get; set; } = CommandLineOptions.DefaultParallelMax;

    public int ParallelMaxHost { get; set; }

    public long? MillisecondsBetweenTransferStarts { get; set; }

    public string? DefaultConfigFile { get; set; }

    public int OpenConfigFileCount { get; set; }

    public bool FirstOptionOfArgument { get; set; }

    public bool ReadingConfigFile { get; set; }

    public bool ReadingConfigFileAsWireText { get; set; }

    public bool ApplyingValueLedByVariableBytes { get; set; }

    public System.Text.Encoding? ConfigFileWireTextEncoding { get; set; }
}
