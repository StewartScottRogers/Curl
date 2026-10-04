using System.Net;
using System.Security.Authentication;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// The settings a command line asks for, filled in by <see cref="CommandLineParser"/>
/// through the rows of <see cref="CommandLineOptionTable"/>. Each option in the table
/// sets one property here. This is only what was asked for: nothing here validates a URL,
/// touches the file system or starts a transfer. The TLS settings are recorded, not applied;
/// the console layer maps them onto the TLS provider.
/// </summary>
public sealed class CommandLineOptions
{
    private readonly List<string> urls = [];
    private readonly List<UrlOutput> urlOutputs = [];
    private readonly List<string> uploadFiles = [];
    private readonly List<string> telnetOptions = [];
    private readonly List<string> mailRecipients = [];
    private readonly List<string> quoteCommands = [];
    private readonly List<string> resolveEntries = [];
    private readonly List<string> connectToEntries = [];
    private readonly List<string> headers = [];
    private readonly List<string> proxyHeaders = [];
    private readonly List<CommandLineCookie> cookies = [];
    private readonly List<FormPartSpecification> formParts = [];
    private readonly CommandLineGlobalState globals;
    private readonly Stack<FormPartSpecification> openMultiparts = new();
    private string? userAwaitingPassword;
    private string? proxyUserAwaitingPassword;
    private HttpAuthSchemes wantedAuthSchemes;
    private HttpAuthSchemes wantedProxyAuthSchemes;
    private bool proxyAnyAuthWanted;
    private bool everyAuthSchemeWanted;

    /// <summary>The single proxy schemes curl 8.21.0's tool picks from, first match wins, after <c>--proxy-anyauth</c>.</summary>
    private static readonly HttpAuthSchemes[] ProxyAuthSchemePrecedence = [HttpAuthSchemes.Negotiate, HttpAuthSchemes.Ntlm, HttpAuthSchemes.Digest];

    /// <summary>The <c>--ech</c> modes libcurl's <c>setopt_ech</c> accepts that take no value, case-sensitive.</summary>
    private static readonly HashSet<string> EchModesWithoutValue = new(["false", "grease", "true", "hard"], StringComparer.Ordinal);

    /// <summary>
    /// Creates the first option group of a command line, with nothing set, and so its own
    /// global settings, which every group <see cref="StartNextGroup"/> adds after it shares.
    /// </summary>
    public CommandLineOptions()
        : this(new CommandLineGlobalState())
    {
    }

    /// <summary>Creates an option group with nothing of its own set, sharing <paramref name="globals"/>, and appends it to their groups.</summary>
    private CommandLineOptions(CommandLineGlobalState globals)
    {
        this.globals = globals;
        globals.Groups.Add(this);
    }

    /// <summary>
    /// Every option group of the command line this group belongs to, in command-line order, this one
    /// included: one more for each <c>-:</c> / <c>--next</c> that started a group (see
    /// <see cref="CommandLineParseResult.Groups"/>).
    /// </summary>
    internal IReadOnlyList<CommandLineOptions> Groups => globals.Groups;

    /// <summary>The group options are being read into now: the last of <see cref="Groups"/>.</summary>
    internal CommandLineOptions CurrentGroup => globals.Groups[^1];

    /// <summary>
    /// <see langword="true"/> while <see cref="CommandLineParser"/> applies a line of a <c>-K</c> file or
    /// of the default config file rather than a command-line argument.
    /// </summary>
    internal bool ReadingConfigFile { get => globals.ReadingConfigFile; set => globals.ReadingConfigFile = value; }

    /// <summary>
    /// Applies <c>-:</c> / <c>--next</c> read into this group, as curl 8.21.0 does: when this group has a
    /// URL, starts a new group after it, whose per-group options start again from nothing while the
    /// global ones stay shared, and which becomes the <see cref="CurrentGroup"/>. Without a URL it is
    /// refused with <see cref="CommandLineRefusal.MissingUrlBeforeNext"/> on the command line, and
    /// ignored in a config file, where curl starts no group (measured 2026-09-28, BL-508 Notes).
    /// </summary>
    /// <param name="spelledOption">The whole argument as typed, such as <c>--next</c> or <c>-s:</c>.</param>
    /// <returns><see langword="null"/> when applied or ignored; otherwise the refusal.</returns>
    internal CommandLineRefusal? StartNextGroup(string spelledOption)
    {
        if (Urls.Count > 0)
        {
            _ = new CommandLineOptions(globals);
            return null;
        }

        return ReadingConfigFile ? null : CommandLineRefusal.MissingUrlBeforeNext(spelledOption, ErrorsHidden);
    }

    /// <summary>
    /// The URLs to transfer, in command-line order: positional arguments and
    /// <c>--url</c> values interleaved as they were given.
    /// </summary>
    public IReadOnlyList<string> Urls => urls;

    /// <summary>
    /// The <c>-T</c> / <c>--upload-file</c> values in command-line order, each unchanged: the Nth is
    /// uploaded to the Nth URL of <see cref="Urls"/>, wherever each was given, and a URL past the end
    /// uploads nothing, as curl 8.21.0 pairs them. An empty value keeps its place and uploads nothing.
    /// </summary>
    public IReadOnlyList<string> UploadFiles => uploadFiles;

    /// <summary>
    /// <see langword="true"/> when <c>-g</c> / <c>--globoff</c> was given and no <c>--no-globoff</c>
    /// came after it: take each URL as written, with <c>UrlGlob.Unglobbed</c>, instead of expanding
    /// <c>{a,b}</c> sets and <c>[1-3]</c> ranges with <c>UrlGlob.TryParse</c>.
    /// </summary>
    public bool GlobOff { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-V</c> / <c>--version</c> was given on the command line. Parsing
    /// stops there, as curl 8.21.0's does, so every option after it is unread; the console prints
    /// <see cref="CurlVersionText"/>'s lines and exits 0 instead of transferring. A <c>version</c>
    /// line in a <c>-K</c> file does not set it: curl ignores it there.
    /// </summary>
    public bool VersionRequested { get => globals.VersionRequested; internal set => globals.VersionRequested = value; }

    /// <summary>
    /// <see langword="true"/> when <c>-h</c> / <c>--help</c> was given on the command line. Parsing stops
    /// there, as curl 8.21.0's does, so every option after it is unread; the console prints
    /// <see cref="CurlHelpText"/>'s lines for <see cref="HelpSubject"/> and exits 0 instead of transferring.
    /// A <c>help</c> line in a <c>-K</c> file does not set it: curl prints that page and carries on, so it
    /// goes to <see cref="ConfigFileHelpSubjects"/> instead.
    /// </summary>
    public bool HelpRequested { get => globals.HelpRequested; private set => globals.HelpRequested = value; }

    /// <summary>
    /// The subject <c>--help</c> was given: its attached value, or else the argument after it, whatever
    /// it looks like; <see langword="null"/> when there was none or it was empty, which asks for the
    /// usage page. Set only with <see cref="HelpRequested"/>.
    /// </summary>
    public string? HelpSubject { get => globals.HelpSubject; private set => globals.HelpSubject = value; }

    /// <summary>
    /// <see langword="true"/> when <c>-M</c> / <c>--manual</c> was given on the command line and no
    /// <c>--no-manual</c> came after it. Parsing stops there, as curl 8.21.0's does; the console prints
    /// <see cref="CurlManual"/>'s lines and exits 0 instead of transferring. A <c>manual</c> line in a
    /// <c>-K</c> file does not set it: curl ignores it there.
    /// </summary>
    public bool ManualRequested { get => globals.ManualRequested; internal set => globals.ManualRequested = value; }

    /// <summary>
    /// <see langword="true"/> when <c>--engine list</c> was given on the command line. Parsing stops there, as
    /// curl 8.21.0's does; the console prints the build-time engine list and exits 0 instead of transferring.
    /// An <c>engine list</c> line in a <c>-K</c> file does not set it: curl ignores the request there.
    /// </summary>
    public bool EngineListRequested { get => globals.EngineListRequested; internal set => globals.EngineListRequested = value; }

    /// <summary>
    /// <see langword="true"/> when <c>--dump-ca-embed</c> was given on the command line. Parsing stops there, as
    /// curl 8.21.0's does; the console writes the embedded CA bundle, which is none (ADR-0151), and exits 0
    /// instead of transferring. A <c>dump-ca-embed</c> line in a <c>-K</c> file does not set it: curl ignores
    /// the request there.
    /// </summary>
    public bool CaEmbedDumpRequested { get => globals.CaEmbedDumpRequested; internal set => globals.CaEmbedDumpRequested = value; }

    /// <summary>
    /// Whether an option has asked for information instead of a transfer (<see cref="VersionRequested"/>,
    /// <see cref="HelpRequested"/>, <see cref="ManualRequested"/>, <see cref="EngineListRequested"/> or
    /// <see cref="CaEmbedDumpRequested"/>), which ends parsing where it stands.
    /// </summary>
    internal bool InformationRequested => VersionRequested || HelpRequested || ManualRequested || AiHelpRequested || EngineListRequested || CaEmbedDumpRequested;

    /// <summary>
    /// <see langword="true"/> when <c>--ai-help</c> was given on the command line. Parsing stops there, as it
    /// does for <c>--help</c>; the console prints <see cref="CurlAiHelpText"/>'s Markdown for
    /// <see cref="AiHelpSubject"/> and exits 0 instead of transferring. An <c>ai-help</c> line in a <c>-K</c>
    /// file does not set it: it is ignored there (ADR-0224).
    /// </summary>
    public bool AiHelpRequested { get => globals.AiHelpRequested; private set => globals.AiHelpRequested = value; }

    /// <summary>
    /// The subject <c>--ai-help</c> was given, read as <c>--help</c> reads its subject; <see langword="null"/>
    /// when there was none or it was empty, which asks for the index. Set only with <see cref="AiHelpRequested"/>.
    /// </summary>
    public string? AiHelpSubject { get => globals.AiHelpSubject; private set => globals.AiHelpSubject = value; }

    /// <summary>Records <c>--ai-help</c> and its subject, an empty one read as none.</summary>
    /// <param name="subject">The subject as given, empty when there was none.</param>
    internal void RequestAiHelp(string subject)
    {
        AiHelpRequested = true;
        AiHelpSubject = subject.Length == 0 ? null : subject;
    }

    /// <summary>Records <c>--help</c> and its subject, an empty one read as none.</summary>
    /// <param name="subject">The subject as given, empty when there was none.</param>
    internal void RequestHelp(string subject)
    {
        HelpRequested = true;
        HelpSubject = subject.Length == 0 ? null : subject;
    }

    /// <summary>
    /// The subjects of the <c>help</c> / <c>-h</c> lines read from <c>-K</c> files, in the order read;
    /// a <see langword="null"/> entry asks for the usage page. curl 8.21.0 prints each page on standard
    /// output as it reads the line and carries on parsing, so the console prints these pages before
    /// anything else (measured 2026-09-27, BL-375).
    /// </summary>
    internal IReadOnlyList<string?> ConfigFileHelpSubjects => globals.ConfigFileHelpSubjects;

    /// <summary>
    /// Moves a help request made by a <c>-K</c> file line to <see cref="ConfigFileHelpSubjects"/>, and
    /// forgets any request for information, as curl does for one made in a <c>-K</c> file.
    /// </summary>
    internal void MoveConfigFileInformationRequests()
    {
        if (HelpRequested)
        {
            globals.ConfigFileHelpSubjects.Add(HelpSubject);
        }

        VersionRequested = false;
        HelpRequested = false;
        HelpSubject = null;
        ManualRequested = false;
        AiHelpRequested = false;
        AiHelpSubject = null;
        EngineListRequested = false;
        CaEmbedDumpRequested = false;
    }

    /// <summary>
    /// <see langword="true"/> when the command line is read as curl's Linux and macOS builds read it, as
    /// UTF-8, so an option value starting with a character in U+2000-U+203F is warned about
    /// (<see cref="CommandLineWarning.ArgumentStartsWithUnicode(string)"/>); <see langword="false"/> as
    /// curl's Windows Schannel build reads it, in the ANSI code page, where it never is. Set by
    /// <see cref="CommandLineParser"/> for the platform it is asked to behave as.
    /// </summary>
    public bool ReadsArgumentsAsUtf8 { get => globals.ReadsArgumentsAsUtf8; internal set => globals.ReadsArgumentsAsUtf8 = value; }

    /// <summary>
    /// <see langword="true"/> when the command line is read as curl's Windows Schannel build reads it: that
    /// build's libcurl has no HTTP/2, HTTP/3, TLS-SRP or SSL session export, so <c>--http2</c>,
    /// <c>--http2-prior-knowledge</c>, <c>--http3</c>, <c>--http3-only</c>, <c>--tlsuser</c>, <c>--tlspassword</c>,
    /// <c>--tlsauthtype</c>, their three <c>--proxy-</c> forms and <c>--ssl-sessions</c> are refused with
    /// <see cref="CommandLineRefusal.InstalledLibcurlDoesNotSupport"/> (<see cref="CommandLineOption.RefusedBySchannelBuild"/>,
    /// ADR-0397). Set by <see cref="CommandLineParser"/> for the platform it is asked to behave as.
    /// </summary>
    public bool ActsAsWindowsSchannelBuild { get => globals.ActsAsWindowsSchannelBuild; internal set => globals.ActsAsWindowsSchannelBuild = value; }

    /// <summary><see langword="true"/> when <c>-s</c> / <c>--silent</c> was given and no <c>--no-silent</c> came after it.</summary>
    public bool Silent { get => globals.Silent; internal set => globals.Silent = value; }

    /// <summary><see langword="true"/> when <c>-S</c> / <c>--show-error</c> was given and no <c>--no-show-error</c> came after it.</summary>
    public bool ShowError { get => globals.ShowError; internal set => globals.ShowError = value; }

    /// <summary>
    /// <see langword="true"/> when <c>--no-progress-meter</c> was given and no <c>--progress-meter</c>
    /// came after it. In curl 8.21.0 it turns the meter off whatever its form, so it outranks
    /// <see cref="ProgressBar"/> in either order.
    /// </summary>
    public bool ProgressMeterOff { get => globals.ProgressMeterOff; internal set => globals.ProgressMeterOff = value; }

    /// <summary>
    /// <see langword="true"/> when <c>-#</c> / <c>--progress-bar</c> was given and no
    /// <c>--no-progress-bar</c> came after it: the meter, when shown, is the bar form.
    /// </summary>
    public bool ProgressBar { get => globals.ProgressBar; internal set => globals.ProgressBar = value; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>-N</c> / <c>--no-buffer</c> and <c>--buffer</c> was
    /// <c>-N</c> or <c>--no-buffer</c>: every block of body bytes is flushed to its output as it is
    /// written, as curl 8.21.0 flushes after each write. <see langword="false"/> otherwise.
    /// </summary>
    public bool NoBuffer { get; internal set; }

    /// <summary>
    /// Which of <c>-v</c> / <c>--verbose</c>, <c>--trace</c> and <c>--trace-ascii</c> came last, or
    /// <see cref="TraceKind.None"/> when none did or <c>--no-verbose</c> came after it.
    /// </summary>
    public TraceKind Trace { get => globals.Trace; private set => globals.Trace = value; }

    /// <summary>
    /// The file the last <c>--trace</c> or <c>--trace-ascii</c> names, <c>-</c> for standard output, while
    /// <see cref="Trace"/> is <see cref="TraceKind.HexDump"/> or <see cref="TraceKind.AsciiDump"/>;
    /// otherwise <see langword="null"/>, as <c>-v</c> writes to standard error.
    /// </summary>
    public string? TraceFile { get => globals.TraceFile; private set => globals.TraceFile = value; }

    /// <summary>
    /// How many times <c>-v</c> was given in a row, 0 to 4, as curl 8.21.0 counts it: the letters of
    /// one argument add up (<c>-vv</c> is 2, and so is <c>-vsv</c>), but a <c>-v</c> or <c>--verbose</c>
    /// that is the first option of its argument starts again at 1 (<c>-v -v</c> is 1, <c>-vv -v</c> is
    /// 1, <c>-vv -sv</c> is 3). A fifth <c>v</c> changes nothing and <c>--no-verbose</c> sets 0. From 2
    /// curl adds transfer and connection IDs and times to its verbose lines, from 3 protocol
    /// details and from 4 every component's trace.
    /// </summary>
    public int Verbosity { get => globals.Verbosity; private set => globals.Verbosity = value; }

    /// <summary>
    /// <see langword="true"/> when every verbose or trace line starts with the time of day: set by
    /// <c>--trace-time</c> and by the second <c>v</c> of <c>-vv</c>, cleared by <c>--no-trace-time</c>,
    /// by <c>--no-verbose</c> and by a <c>-v</c> or <c>--verbose</c> that is the first option of its
    /// argument (<c>--trace-time -v</c> shows no times; <c>--trace-time -sv</c> and <c>-v --trace-time</c> do).
    /// <c>--trace-config time</c> (or <c>all</c>) also sets it, and that a first <c>-v</c> does not
    /// clear: <c>--trace-config time -v</c> shows times (measured 2026-10-01, BL-649 Notes).
    /// </summary>
    public bool TraceTime { get => globals.TraceTime || globals.TraceConfigTime; internal set => globals.TraceTime = value; }

    /// <summary>
    /// <see langword="true"/> when every verbose or trace line carries its transfer and connection
    /// IDs, <c>[0-0] </c>: set by <c>--trace-ids</c> and by the second <c>v</c> of <c>-vv</c>, cleared
    /// as <see cref="TraceTime"/> is (measured 2026-09-29, BL-648 Notes); set by
    /// <c>--trace-config ids</c> (or <c>all</c>) as <see cref="TraceTime"/> is by <c>time</c>.
    /// </summary>
    public bool TraceIds { get => globals.TraceIds || globals.TraceConfigIds; internal set => globals.TraceIds = value; }

    /// <summary>
    /// The trace component names <c>--trace-config</c> turned on and did not turn off again, in lower
    /// case: <c>tls</c>, <c>http/1</c>, <c>dns</c>, <c>doh</c> and the rest, with <c>all</c> standing for
    /// every component. Names curl does not know are kept too, since curl 8.21.0 ignores them silently.
    /// <c>-vv</c> adds <c>setup</c>, <c>-vvv</c> <c>read</c> and <c>write</c>, and <c>-vvvv</c> <c>all</c>; a
    /// first <c>-v</c> takes those out again unless a <c>--trace-config</c> named them since, and
    /// <c>--no-verbose</c> and <c>--trace-config -all</c> empty the set (measured, BL-1103 Notes).
    /// </summary>
    public IReadOnlySet<string> TraceComponents => globals.TraceComponents;

    /// <summary>
    /// The names in <see cref="TraceComponents"/> that only <c>-vv</c> and up put there, not a
    /// <c>--trace-config</c>: <c>all</c> here came from <c>-vvvv</c>, which curl 8.21.0 does not let
    /// write the <c>[SOCKS]</c> lines that <c>--trace-config all</c> writes (measured, BL-1191 Notes).
    /// </summary>
    public IReadOnlySet<string> VerbosityTraceComponents => globals.VerbosityTraceComponents;

    /// <summary>
    /// Applies <c>--trace-ids</c>, or <c>--no-trace-ids</c> when <paramref name="on"/> is
    /// <see langword="false"/>, which also turns off <c>--trace-config ids</c>.
    /// </summary>
    /// <param name="on"><see langword="false"/> for <c>--no-trace-ids</c>.</param>
    internal void SetTraceIds(bool on)
    {
        TraceIds = on;
        globals.TraceConfigIds &= on;
    }

    /// <summary>
    /// Applies <c>--trace-time</c>, or <c>--no-trace-time</c> when <paramref name="on"/> is
    /// <see langword="false"/>, which also turns off <c>--trace-config time</c>.
    /// </summary>
    /// <param name="on"><see langword="false"/> for <c>--no-trace-time</c>.</param>
    internal void SetTraceTime(bool on)
    {
        TraceTime = on;
        globals.TraceConfigTime &= on;
    }

    /// <summary>
    /// Applies <c>--trace-config &lt;list&gt;</c> as curl 8.21.0 reads it (measured 2026-10-01, BL-649
    /// Notes): the list splits at commas, a name after a comma may start with blanks, a leading
    /// <c>-</c> turns the name off and a leading <c>+</c> is dropped, and names are case-insensitive.
    /// <c>ids</c> and <c>time</c> turn <see cref="TraceIds"/> and <see cref="TraceTime"/> on or off,
    /// <c>all</c> both of them and every component; any other name, known to curl or not, goes into
    /// or out of <see cref="TraceComponents"/>. Nothing is ever refused or warned about.
    /// </summary>
    /// <param name="list">The option's value, possibly empty.</param>
    internal void ApplyTraceConfig(string list)
    {
        string[] tokens = list.Split(',');
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = index == 0 ? tokens[index] : tokens[index].TrimStart(' ', '\t');
            bool on = !token.StartsWith('-');
            string name = token.TrimStart('-', '+').ToLowerInvariant();
            if (name.Length > 0)
            {
                ApplyTraceConfigName(name, on);
            }
        }
    }

    /// <summary>Turns one <c>--trace-config</c> name on or off: see <see cref="ApplyTraceConfig"/>.</summary>
    private void ApplyTraceConfigName(string name, bool on)
    {
        switch (name)
        {
            case "ids":
                SetTraceConfigIds(on);
                break;
            case "time":
                SetTraceConfigTime(on);
                break;
            case "all":
                SetTraceConfigIds(on);
                SetTraceConfigTime(on);
                SetTraceComponent(name, on);
                break;
            default:
                SetTraceComponent(name, on);
                break;
        }
    }

    /// <summary>Turns <c>--trace-config ids</c> on or off, with <see cref="TraceIds"/>.</summary>
    private void SetTraceConfigIds(bool on)
    {
        TraceIds = on;
        globals.TraceConfigIds = on;
    }

    /// <summary>Turns <c>--trace-config time</c> on or off, with <see cref="TraceTime"/>.</summary>
    private void SetTraceConfigTime(bool on)
    {
        TraceTime = on;
        globals.TraceConfigTime = on;
    }

    /// <summary>
    /// Adds <paramref name="name"/> to <see cref="TraceComponents"/>, or takes it out when <paramref name="on"/>
    /// is <see langword="false"/>; <c>-all</c> takes every name out, the <c>-vv</c> ones included (measured
    /// 2026-10-02: <c>-vv --trace-config -all</c> writes no <c>[SETUP]</c> lines, BL-1103 Notes).
    /// </summary>
    private void SetTraceComponent(string name, bool on)
    {
        globals.VerbosityTraceComponents.Remove(name);
        if (on)
        {
            globals.TraceComponents.Add(name);
        }
        else if (name == "all")
        {
            ClearTraceComponents();
        }
        else
        {
            globals.TraceComponents.Remove(name);
        }
    }

    /// <summary>Empties <see cref="TraceComponents"/>, as <c>--no-verbose</c> and <c>--trace-config -all</c> do.</summary>
    private void ClearTraceComponents()
    {
        globals.TraceComponents.Clear();
        globals.VerbosityTraceComponents.Clear();
    }

    /// <summary>
    /// The trace components curl 8.21.0 turns on at each <see cref="Verbosity"/> (measured 2026-10-02 with
    /// <c>Record-CurlExchange.ps1</c>, BL-1103 Notes): <c>-vv</c> writes the <c>[SETUP]</c> lines and the protocols' (<c>[FTP]</c>, measured BL-1162), <c>-vvv</c>
    /// adds <c>[READ]</c> and <c>[WRITE]</c>, and <c>-vvvv</c> every component, as <c>all</c> does.
    /// </summary>
    /// <param name="verbosity">The verbosity just reached, 2 to 4.</param>
    /// <returns>The component names to turn on.</returns>
    private static string[] VerbosityTraceComponentsAt(int verbosity) => verbosity switch
    {
        2 => ["setup", "protocol"],
        3 => ["read", "write"],
        _ => ["all"],
    };

    /// <summary>Turns on the components <paramref name="verbosity"/> brings, remembering them as <c>-v</c>'s own.</summary>
    private void AddVerbosityTraceComponents(int verbosity)
    {
        foreach (string name in VerbosityTraceComponentsAt(verbosity))
        {
            if (globals.TraceComponents.Add(name))
            {
                globals.VerbosityTraceComponents.Add(name);
            }
        }
    }

    /// <summary>Takes out the components <c>-vv</c> and up turned on, as a first <c>-v</c> does (<c>-vv -v</c> writes no <c>[SETUP]</c>).</summary>
    private void RemoveVerbosityTraceComponents()
    {
        globals.TraceComponents.ExceptWith(globals.VerbosityTraceComponents);
        globals.VerbosityTraceComponents.Clear();
    }

    /// <summary>
    /// The file the last <c>--stderr</c> names, to which curl writes what it would write to standard
    /// error: <c>-</c> for standard output; <see langword="null"/> when none was given. An empty name is
    /// kept, not refused: curl 8.21.0 fails to open it, warns and carries on writing to standard error,
    /// which the console layer does when it opens the file.
    /// </summary>
    public string? StandardErrorFile { get => globals.StandardErrorFile; private set => globals.StandardErrorFile = value; }

    /// <summary>
    /// Every <c>--stderr</c> read, in command-line order, each with the point where curl opens its
    /// file; empty when none was given. The last one's file is <see cref="StandardErrorFile"/>.
    /// </summary>
    public IReadOnlyList<StandardErrorRedirect> StandardErrorRedirects => globals.StandardErrorRedirects;

    /// <summary>
    /// How much of Curl's own diagnostic log the run asks for (ADR-0222): the level the last
    /// <c>--log-level</c> named; <see cref="Protocol.Abstractions.DiagnosticLogLevel.Info"/> when there was none but a
    /// <c>--log-file</c> was given; otherwise <see cref="Protocol.Abstractions.DiagnosticLogLevel.None"/>. Global: it
    /// holds across <c>-:</c> / <c>--next</c>.
    /// </summary>
    public DiagnosticLogLevel DiagnosticLogLevel =>
        globals.DiagnosticLogLevelGiven ?? (DiagnosticLogFile is null ? DiagnosticLogLevel.None : DiagnosticLogLevel.Info);

    /// <summary>
    /// The file the last <c>--log-file</c> names, to which the diagnostic log goes instead of standard
    /// error; <see langword="null"/> when none was given. Parsing never opens it; the console does.
    /// </summary>
    public string? DiagnosticLogFile { get => globals.DiagnosticLogFile; internal set => globals.DiagnosticLogFile = value; }

    /// <summary>Records the level a <c>--log-level</c> named, the last one winning.</summary>
    internal void SetDiagnosticLogLevel(DiagnosticLogLevel level) => globals.DiagnosticLogLevelGiven = level;

    /// <summary>
    /// The <c>-o</c> / <c>--output</c> file name of each entry of <see cref="UrlOutputs"/>, in the same
    /// order and up to the last entry that has one, <see langword="null"/> for an entry before it that
    /// has none: the Nth element is the <c>-o</c> file paired with the Nth URL, and a URL past the end
    /// has none. Empty when no <c>-o</c> was given.
    /// </summary>
    public IReadOnlyList<string?> OutputFiles =>
        urlOutputs[..(urlOutputs.FindLastIndex(output => output.FileName is not null) + 1)].ConvertAll(output => output.FileName);

    /// <summary>
    /// Where each URL's body goes, one <see cref="UrlOutput"/> per URL or output option, in the order
    /// curl 8.21.0 pairs them: the Nth URL with the Nth <c>-o</c>, <c>-O</c>, <c>--out-null</c> or kept
    /// <c>--no-remote-name</c>. Entries past the last URL have no <see cref="UrlOutput.Url"/>.
    /// </summary>
    public IReadOnlyList<UrlOutput> UrlOutputs => urlOutputs;

    /// <summary>
    /// <see langword="true"/> when <c>--remote-name-all</c> was given and no <c>--no-remote-name-all</c>
    /// came after it. It applies to each URL or output option read while it is on, not to earlier ones.
    /// </summary>
    public bool RemoteNameAll { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-J</c> / <c>--remote-header-name</c> was given and no
    /// <c>--no-remote-header-name</c> came after it: a remote-named file takes its name from the
    /// <c>Content-Disposition</c> header when there is one.
    /// </summary>
    public bool RemoteHeaderName { get; internal set; }

    /// <summary>
    /// The <c>--output-dir</c> directory, verbatim and unchecked; <see langword="null"/> when not given.
    /// The last value wins.
    /// </summary>
    public string? OutputDirectory { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--create-dirs</c> was given and no <c>--no-create-dirs</c> came
    /// after it: missing directories in an output path are created.
    /// </summary>
    public bool CreateDirectories { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>--clobber</c> and <c>--no-clobber</c> was <c>--clobber</c>:
    /// an existing output file is overwritten, even one a <c>-J</c> name picked. <see langword="false"/> for
    /// <c>--no-clobber</c>: an existing output file is left alone and the body goes to the first free
    /// <c>&lt;name&gt;.1</c> ... <c>&lt;name&gt;.99</c>, as curl 8.21.0 does. <see langword="null"/> when
    /// neither was given: an <c>-o</c> or <c>-O</c> file is overwritten and a <c>-J</c> file is not.
    /// </summary>
    public bool? Clobber { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>--skip-existing</c> and <c>--no-skip-existing</c> was
    /// <c>--skip-existing</c>: a transfer whose <c>-o</c> or <c>-O</c> file already exists is not
    /// performed, as curl 8.21.0 skips it (BL-493).
    /// </summary>
    public bool SkipExisting { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>--remove-on-error</c> and <c>--no-remove-on-error</c> was
    /// <c>--remove-on-error</c>: a transfer that fails removes the <c>-o</c> or <c>-O</c> file it opened, as
    /// curl 8.21.0 does (BL-494). It cannot be combined with <c>-C</c>/<c>--continue-at</c>.
    /// </summary>
    public bool RemoveOnError { get; internal set; }

    /// <summary>
    /// The <c>-w</c> / <c>--write-out</c> template, unexpanded; <see langword="null"/> when not given or
    /// when the last <c>-w @file</c> named an empty file. The last value wins. An <c>@file</c> or
    /// <c>@-</c> value is the file's (or standard input's) text with every carriage return, line feed and
    /// NUL removed, as curl 8.21.0 reads it.
    /// </summary>
    public string? WriteOut { get; internal set; }

    /// <summary>
    /// The request body built from every <c>-d</c> / <c>--data</c>, <c>--data-ascii</c>, <c>--data-binary</c>,
    /// <c>--data-raw</c>, <c>--data-urlencode</c> and <c>--json</c> value, as bytes; <see langword="null"/>
    /// when none was given. An empty value is empty data, not a refusal. The pieces are joined in
    /// command-line order, as in curl 8.21.0: a <c>--json</c> piece is appended as it is, and any other
    /// piece after a single <c>&amp;</c> when the body so far is not empty. Text is taken as UTF-8.
    /// A <c>-d</c> or <c>--data-ascii</c> value <c>@file</c> (or <c>@-</c>) contributes the file's (or
    /// standard input's) bytes with every carriage return, line feed and NUL removed; a
    /// <c>--data-binary</c> or <c>--json</c> one contributes them unchanged; <c>--data-raw</c> never
    /// reads a file. With <see cref="DataInQuery"/> the body is sent as the URL query instead
    /// (see <see cref="QueryUrl"/>).
    /// </summary>
    public ReadOnlyMemory<byte>? PostData { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>--json</c> was given at least once: curl 8.21.0 then sends
    /// <c>Content-Type: application/json</c> and <c>Accept: application/json</c>, even when a later
    /// <c>-d</c> adds to the body.
    /// </summary>
    public bool SendsJson { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>-G</c> / <c>--get</c> was given and no <c>--no-get</c> came after it:
    /// the request is a GET and <see cref="PostData"/>, when given, is sent as the URL query
    /// (see <see cref="QueryUrl"/>).
    /// </summary>
    public bool DataInQuery { get; internal set; }

    /// <summary>
    /// The <c>--url-query</c> values joined in command-line order with a <c>&amp;</c> between each
    /// two, even when one is empty, as curl 8.21.0 does; <see langword="null"/> when not given.
    /// Each value is encoded as <c>--data-urlencode</c> encodes it, except that one starting with
    /// <c>+</c> is kept verbatim without its <c>+</c>. Appended to the URL by <see cref="QueryUrl"/>.
    /// </summary>
    public string? UrlQuery { get; private set; }

    /// <summary>
    /// The <c>-D</c> / <c>--dump-header</c> file, verbatim and unchecked; <see langword="null"/> when
    /// not given. <c>-</c> means standard output. Nothing is opened or created here. The last value
    /// wins, as in curl 8.21.0.
    /// </summary>
    public string? DumpHeaderFile { get; internal set; }

    /// <summary>
    /// The <c>--etag-save</c> file, verbatim and unchecked; <see langword="null"/> when not given. <c>-</c>
    /// means standard output. The transfer writes the <c>ETag</c> of a 2xx or 3xx response there; nothing
    /// is opened here. The last value wins.
    /// </summary>
    public string? EtagSaveFile { get; internal set; }

    /// <summary>
    /// The <c>--etag-compare</c> file, verbatim and unchecked; <see langword="null"/> when not given. The
    /// transfer sends its content as <c>If-None-Match</c>; nothing is read here. The last value wins.
    /// </summary>
    public string? EtagCompareFile { get; internal set; }

    /// <summary>
    /// The <c>--alt-svc</c> cache file, verbatim and unchecked; <see langword="null"/> when not given. An
    /// empty value turns alt-svc on without a file, as curl 8.21.0 accepts it. The transfer reads and writes
    /// the file; nothing is opened here. The last value wins.
    /// </summary>
    public string? AltSvcFile { get; internal set; }

    /// <summary>
    /// The <c>--hsts</c> cache file, verbatim and unchecked; <see langword="null"/> when not given. An
    /// empty value is accepted and names no file, as curl 8.21.0 accepts it, and a value that looks like a
    /// flag draws no warning (measured 2026-09-29, BL-621 Notes). The transfer reads and writes the file;
    /// nothing is opened here. The last value wins.
    /// </summary>
    public string? HstsFile { get; internal set; }

    /// <summary>
    /// The <c>-u</c> / <c>--user</c> value split at its first colon into user name and password;
    /// <see langword="null"/> when not given. A value with no colon that does not start with <c>;</c>
    /// is a user name whose password <see cref="CommandLineParser"/> asks for through its
    /// <see cref="IPasswordPrompt"/> once the whole command line is read; <c>;</c> with no colon
    /// (<c>-u ;opt</c>) is a user name with an empty password, as in curl 8.21.0.
    /// </summary>
    public NetworkCredential? Credentials { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <c>-n</c> / <c>--netrc</c> was given and no <c>--no-netrc</c> came after it.
    /// Giving it more than once has no extra effect.
    /// </summary>
    public bool NetrcRequested { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--netrc-optional</c> was given and no <c>--no-netrc-optional</c> came
    /// after it. It makes the netrc file optional even beside <c>-n</c> or <c>--netrc-file</c>, which curl
    /// 8.21.0 accepts in either order although its manual calls them mutually exclusive (measured
    /// 2026-09-28, BL-504 Notes).
    /// </summary>
    public bool NetrcOptionalRequested { get; internal set; }

    /// <summary>
    /// The <c>--netrc-file</c> value, which curl 8.21.0 requires to exist (a directory passes) when the option
    /// is read; <see langword="null"/> when not given. The last value wins, and <c>--no-netrc</c> does not
    /// clear it.
    /// </summary>
    public string? NetrcFile { get; internal set; }

    /// <summary>
    /// Whether the transfer reads a netrc file, as curl 8.21.0 decides it: <see cref="NetrcUse.Optional"/>
    /// when <see cref="NetrcOptionalRequested"/>, otherwise <see cref="NetrcUse.Required"/> when
    /// <see cref="NetrcRequested"/> or a <see cref="NetrcFile"/> is named, otherwise <see cref="NetrcUse.Ignored"/>.
    /// </summary>
    public NetrcUse NetrcUse =>
        NetrcOptionalRequested ? NetrcUse.Optional
        : NetrcRequested || NetrcFile is not null ? NetrcUse.Required
        : NetrcUse.Ignored;

    /// <summary>
    /// The <c>-U</c> / <c>--proxy-user</c> value split at its first colon into user name and
    /// password, for the proxy; <see langword="null"/> when not given. A user with no password is
    /// asked for as <see cref="Credentials"/> is, with curl 8.21.0's proxy prompt. An empty value is
    /// accepted: curl 8.21.0 asks for the password of the user <c>''</c>.
    /// </summary>
    public NetworkCredential? ProxyCredentials { get; private set; }

    /// <summary>
    /// The HTTP authentication schemes to allow for the origin, as curl 8.21.0's tool asks libcurl
    /// for them: <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c> and <c>--negotiate</c> add their scheme and their <c>--no-</c> spellings
    /// remove it; <c>--anyauth</c> replaces the set with every scheme; <c>--oauth2-bearer</c> adds
    /// Bearer. <see cref="HttpAuthSchemes.Bearer"/> is only ever allowed with a
    /// <see cref="BearerToken"/>, so <c>--anyauth</c> alone gives <see cref="HttpAuthSchemes.Any"/>.
    /// When nothing is left, which is also when no scheme option was given, the set is
    /// <see cref="HttpAuthSchemes.Basic"/>, libcurl's default.
    /// </summary>
    /// <remarks>
    /// Measured with the reference curl 8.21.0 (<c>Record-CurlExchange.ps1</c>, a 401 offering Bearer
    /// and Basic, 2026-09-26): <c>-u u:p --no-basic</c> and <c>-u u:p --digest --no-digest</c> send
    /// <c>Basic dTpw</c> at once; <c>-u u:p --basic --no-basic --digest</c> sends nothing;
    /// <c>-u u:p --oauth2-bearer tok --basic</c> sends nothing, then <c>Bearer tok</c>;
    /// <c>-u u:p --anyauth --basic</c> and <c>-u u:p --anyauth</c> send nothing, then
    /// <c>Basic dTpw</c>; <c>--oauth2-bearer tok --anyauth</c> sends nothing, then <c>Bearer tok</c>;
    /// <c>--oauth2-bearer tok --no-basic</c> sends <c>Bearer tok</c> at once. Against a plain 200
    /// (2026-09-26): <c>-u u:p --ntlm</c> sends an NTLM type-1 message at once; <c>-u u:p --negotiate</c>,
    /// <c>--basic --ntlm</c> and <c>--ntlm --negotiate</c> send nothing; <c>--ntlm --no-ntlm</c> and
    /// <c>--negotiate --no-negotiate</c> send <c>Basic dTpw</c>. See ADR-0026.
    /// </remarks>
    public HttpAuthSchemes AuthSchemes
    {
        get
        {
            HttpAuthSchemes allowed = BearerToken is null ? wantedAuthSchemes & ~HttpAuthSchemes.Bearer : wantedAuthSchemes;
            return allowed == HttpAuthSchemes.None ? HttpAuthSchemes.Basic : allowed;
        }
    }

    /// <summary>
    /// The HTTP authentication schemes to allow for the proxy, as curl 8.21.0's tool asks libcurl
    /// for them. Unlike <see cref="AuthSchemes"/>, the switches do not add up: <c>--proxy-basic</c>,
    /// <c>--proxy-digest</c>, <c>--proxy-ntlm</c>, <c>--proxy-negotiate</c> and <c>--proxy-anyauth</c>
    /// each set a switch (their <c>--no-</c> spellings clear it), and the set is one scheme, picked
    /// in the order <c>--proxy-anyauth</c> (<see cref="HttpAuthSchemes.Any"/>), <c>--proxy-negotiate</c>,
    /// <c>--proxy-ntlm</c>, <c>--proxy-digest</c>, whatever order they came in. When none of those is
    /// on the set is <see cref="HttpAuthSchemes.Basic"/>, which is also libcurl's default.
    /// </summary>
    /// <remarks>
    /// Measured with the reference curl 8.21.0 (<c>Record-CurlExchange.ps1</c> as the proxy,
    /// <c>-U u:p</c>, 2026-09-28): with no switch, <c>--proxy-basic --no-proxy-basic</c> or
    /// <c>--proxy-digest --no-proxy-digest</c> curl sends <c>Basic dTpw</c> at once;
    /// <c>--proxy-digest --proxy-ntlm</c> sends an NTLM type-1 message at once;
    /// <c>--proxy-ntlm --proxy-negotiate</c> sends nothing; against a <c>407</c> offering only Basic,
    /// <c>--proxy-digest --proxy-basic</c> and <c>--proxy-basic --proxy-digest</c> give up with the
    /// <c>407</c>, while <c>--proxy-anyauth --proxy-basic</c> answers with <c>Basic dTpw</c>. See BL-601.
    /// </remarks>
    public HttpAuthSchemes ProxyAuthSchemes
    {
        get
        {
            if (proxyAnyAuthWanted)
            {
                return HttpAuthSchemes.Any;
            }

            foreach (HttpAuthSchemes scheme in ProxyAuthSchemePrecedence)
            {
                if ((wantedProxyAuthSchemes & scheme) != 0)
                {
                    return scheme;
                }
            }

            return HttpAuthSchemes.Basic;
        }
    }

    /// <summary>
    /// The last <c>--oauth2-bearer</c> token; <see langword="null"/> when not given. An empty value is
    /// refused as blank. While it is set, a <c>-u</c> user with no password is not prompted for.
    /// </summary>
    public string? BearerToken { get; private set; }

    /// <summary>
    /// The last <c>--aws-sigv4</c> value, verbatim, the empty string included, which curl 8.21.0
    /// signs as <c>aws:amz</c> (measured, BL-629 Notes); <see langword="null"/> when not given.
    /// While it is set, every request to the origin is signed with AWS Signature Version 4 in
    /// place of <see cref="AuthSchemes"/>.
    /// </summary>
    public string? AwsSigV4 { get; internal set; }

    /// <summary>
    /// The last <c>-x</c> / <c>--proxy</c>, <c>--proxy1.0</c>, <c>--socks4</c>, <c>--socks4a</c>, <c>--socks5</c> or
    /// <c>--socks5-hostname</c> value, with the kind of proxy that option names; <see langword="null"/>
    /// when none was given. curl 8.21.0 keeps one proxy: the last of these options wins, value and kind
    /// together, and a scheme in the value outranks the option's kind. An empty <c>-x ''</c> is kept:
    /// it asks for no proxy at all, the environment's included.
    /// </summary>
    /// <remarks>
    /// Measured with the reference curl 8.21.0 on 2026-09-28 (<c>Record-CurlExchange.ps1</c>, reading the
    /// CONNECT line a <c>-p</c> tunnel sent; BL-612 Notes): <c>--proxy1.0 A</c>, <c>-x A --proxy1.0 B</c>
    /// and <c>--socks5 A --proxy1.0 B</c> send <c>CONNECT … HTTP/1.0</c>; <c>--proxy1.0 A -x B</c> sends
    /// <c>HTTP/1.1</c>; <c>--proxy1.0 A --socks5 B</c> and <c>--proxy1.0 socks5://A</c> speak SOCKS5.
    /// </remarks>
    public CommandLineProxy? Proxy { get; private set; }

    /// <summary>
    /// The last <c>--preproxy</c> value, verbatim: the SOCKS proxy to pass through before
    /// <see cref="Proxy"/>; <see langword="null"/> when not given. An empty value is refused as blank.
    /// Parsing it as a proxy URL is the proxy selector's job.
    /// </summary>
    public string? PreProxy { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--socks5-basic</c> was given and no <c>--no-socks5-basic</c> came
    /// after it: allow user name and password authentication with a SOCKS5 proxy.
    /// </summary>
    public bool Socks5BasicAuth { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--socks5-gssapi</c> was given and no <c>--no-socks5-gssapi</c> came
    /// after it: allow GSS-API authentication with a SOCKS5 proxy.
    /// </summary>
    public bool Socks5GssapiAuth { get; internal set; }

    /// <summary>
    /// The last <c>--socks5-gssapi-service</c> value, verbatim: the service name for SOCKS5 GSS-API
    /// authentication; <see langword="null"/> when not given. An empty value is accepted, as curl 8.21.0
    /// accepts it.
    /// </summary>
    public string? Socks5GssapiServiceName { get; internal set; }

    /// <summary>
    /// The last <c>--delegation</c> value, read without regard to case; <see cref="Cli.GssApiDelegation.None"/>
    /// when not given or when the last value was none of <c>none</c>, <c>policy</c> and <c>always</c>.
    /// </summary>
    public GssApiDelegation GssApiDelegation { get; internal set; }

    /// <summary>
    /// The last <c>--service-name</c> value, verbatim: the service name SPNEGO, Kerberos and the SASL
    /// mechanisms use in place of the protocol's default; <see langword="null"/> when not given. An empty
    /// value is refused as blank, as curl 8.21.0 refuses it.
    /// </summary>
    public string? ServiceName { get; internal set; }

    /// <summary>
    /// The last <c>--proxy-service-name</c> value, verbatim: the service name SPNEGO uses with a proxy in
    /// place of <c>HTTP</c>; <see langword="null"/> when not given. An empty value is refused as blank, as
    /// curl 8.21.0 refuses it.
    /// </summary>
    public string? ProxyServiceName { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--socks5-gssapi-nec</c> was given and no
    /// <c>--no-socks5-gssapi-nec</c> came after it: leave the GSS-API protection negotiation
    /// unprotected, as the NEC SOCKS5 server expects.
    /// </summary>
    public bool Socks5GssapiNec { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--haproxy-protocol</c> was given and no <c>--no-haproxy-protocol</c>
    /// came after it: send a HAProxy PROXY protocol v1 header first on the connection.
    /// </summary>
    public bool HaproxyProtocol { get; internal set; }

    /// <summary>
    /// The last <c>--haproxy-clientip</c> value, verbatim and unvalidated: the client address to put in
    /// the HAProxy PROXY header; <see langword="null"/> when not given. An empty value is refused as blank.
    /// </summary>
    public string? HaproxyClientIp { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--suppress-connect-headers</c> was given and no
    /// <c>--no-suppress-connect-headers</c> came after it: leave the proxy's CONNECT response headers
    /// out of the headers shown and saved.
    /// </summary>
    public bool SuppressConnectHeaders { get; internal set; }

    /// <summary>
    /// The last <c>--noproxy</c> value, verbatim: the hosts to reach without a proxy. Empty is
    /// accepted. <see langword="null"/> when not given. Matching hosts against it is the proxy
    /// selector's job.
    /// </summary>
    public string? NoProxy { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-p</c> / <c>--proxytunnel</c> was given and no
    /// <c>--no-proxytunnel</c> came after it: tunnel through an HTTP proxy with CONNECT.
    /// </summary>
    public bool ProxyTunnel { get; internal set; }

    /// <summary>Every <c>-t</c> / <c>--telnet-option</c> value, verbatim and unvalidated, in command-line order.</summary>
    public IReadOnlyList<string> TelnetOptions => telnetOptions;

    /// <summary>
    /// Every <c>--resolve</c> value (<c>[+]host:port:addr[,addr]...</c>, or <c>-host:port</c> to drop an
    /// entry), verbatim and unvalidated, in command-line order. curl 8.21.0 checks the syntax only when a
    /// transfer starts, failing it with exit code 49 (<c>Could not parse CURLOPT_RESOLVE entry</c>), so the
    /// parser never refuses one.
    /// </summary>
    public IReadOnlyList<string> ResolveEntries => resolveEntries;

    /// <summary>
    /// Every <c>--connect-to</c> value (<c>host1:port1:host2:port2</c>, any part possibly empty), verbatim
    /// and unvalidated, in command-line order. curl 8.21.0 reads an entry only when a transfer starts, so
    /// the parser never refuses one.
    /// </summary>
    public IReadOnlyList<string> ConnectToEntries => connectToEntries;

    /// <summary>
    /// The last <c>--interface</c>, split by its <c>if!</c>, <c>host!</c> or <c>ifhost!</c> prefix;
    /// <see langword="null"/> when not given. An empty value is refused as blank; any other is accepted, a
    /// value libcurl would refuse included (<see cref="InterfaceBinding.IsMalformed"/>).
    /// </summary>
    public InterfaceBinding? Interface { get; internal set; }

    /// <summary>
    /// The last <c>--local-port</c> range; <see langword="null"/> when not given. A value that is not
    /// <c>num</c> or <c>num-num</c> within 0 to 65535, low to high, is refused as badly used.
    /// </summary>
    public LocalPortRange? LocalPorts { get; internal set; }

    /// <summary>
    /// The last <c>--dns-servers</c> value (<c>host[:port]</c> entries separated by commas), verbatim and
    /// unvalidated; <see langword="null"/> when not given. An empty value is refused as blank. A c-ares
    /// build of curl checks the list only when it resolves a host name, failing that transfer with exit
    /// code 43, so the parser accepts any other value (measured 2026-09-28, BL-643 Notes).
    /// </summary>
    public string? DnsServers { get; internal set; }

    /// <summary>
    /// The last <c>--dns-interface</c>, the network interface name DNS queries go out on, verbatim;
    /// <see langword="null"/> when not given. An empty value is refused as blank; any other name is
    /// accepted without looking it up, as a c-ares build of curl does.
    /// </summary>
    public string? DnsInterface { get; internal set; }

    /// <summary>
    /// The last <c>--dns-ipv4-addr</c>, the local IPv4 address DNS queries are sent from, verbatim and
    /// unvalidated; <see langword="null"/> when not given. An empty value is refused as blank. A c-ares
    /// build of curl checks the address only when it resolves a host name, failing that transfer with
    /// exit code 43.
    /// </summary>
    public string? DnsIPv4Address { get; internal set; }

    /// <summary>
    /// The last <c>--dns-ipv6-addr</c>, the local IPv6 address DNS queries are sent from, verbatim and
    /// unvalidated; <see langword="null"/> when not given. An empty value is refused as blank. A c-ares
    /// build of curl checks the address only when it resolves a host name, failing that transfer with
    /// exit code 43.
    /// </summary>
    public string? DnsIPv6Address { get; internal set; }

    /// <summary>
    /// The last <c>--doh-url</c>, the DNS-over-HTTPS server every host name is resolved through, verbatim
    /// and unchecked; <see langword="null"/> when not given, or when the last value was empty, which curl
    /// 8.21.0 accepts and which turns DoH off again (BL-642). A value curl cannot use as a DoH URL fails
    /// each transfer with exit 6 when it resolves, not the parse.
    /// </summary>
    public string? DohUrl { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--doh-insecure</c> was given and no <c>--no-doh-insecure</c> came after
    /// it: skip verification of the DoH server's certificate. <c>-k</c> never reaches the DoH server.
    /// </summary>
    public bool DohInsecure { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--doh-cert-status</c> was given and no <c>--no-doh-cert-status</c> came
    /// after it: require a good OCSP response stapled to the DoH server's certificate, as
    /// <see cref="RequireCertificateStatus"/> does for the transfer's server.
    /// </summary>
    public bool DohCertificateStatus { get; internal set; }

    /// <summary>
    /// The path of the Unix domain socket to connect through, from whichever of <c>--unix-socket</c> and
    /// <c>--abstract-unix-socket</c> came last; <see langword="null"/> when neither was given. curl 8.21.0
    /// refuses an empty path as blank and accepts any other without looking at it, on Windows too (measured
    /// 2026-09-28, BL-506 Notes).
    /// </summary>
    public string? UnixSocketPath { get; private set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="UnixSocketPath"/> came from <c>--abstract-unix-socket</c>, so
    /// it names a socket in the abstract namespace rather than a file.
    /// </summary>
    public bool UnixSocketIsAbstract { get; private set; }

    /// <summary>
    /// The <c>--tftp-blksize</c> value as given, unclamped, except that a value past
    /// <see cref="int.MaxValue"/> (accepted where a C <c>long</c> is 64 bits) is recorded as
    /// <see cref="int.MaxValue"/>; <see langword="null"/> when not given. The TFTP handler clamps
    /// it to 8-65464, so the two mean the same.
    /// </summary>
    public int? TftpBlockSize { get; internal set; }

    /// <summary><see langword="true"/> when <c>--tftp-no-options</c> was given and no <c>--no-tftp-no-options</c> came after it.</summary>
    public bool TftpNoOptions { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>--disable-epsv</c>, <c>--epsv</c> and their <c>--no-</c>
    /// spellings turned <c>EPSV</c> off (<c>--disable-epsv</c> or <c>--no-epsv</c>); <see langword="false"/> otherwise.
    /// </summary>
    public bool FtpDisableEpsv { get; internal set; }

    /// <summary>
    /// <see langword="false"/> when <c>--no-ftp-skip-pasv-ip</c> was given and no <c>--ftp-skip-pasv-ip</c> came
    /// after it; <see langword="true"/> otherwise, as curl 8.21.0 skips the <c>PASV</c> reply's address by default.
    /// </summary>
    public bool FtpSkipPasvIp { get; internal set; } = true;

    /// <summary>
    /// The last <c>--ftp-method</c> value, read without regard to case; <see cref="FtpFileMethod.MultiCwd"/> when
    /// not given or when the last value was none of <c>multicwd</c>, <c>nocwd</c> and <c>singlecwd</c>.
    /// </summary>
    public FtpFileMethod FtpFileMethod { get; internal set; }

    /// <summary><see langword="true"/> once any <c>--ftp-method</c> was given, which <c>--libcurl</c> writes as <c>CURLOPT_FTP_FILEMETHOD</c> even for <c>multicwd</c>.</summary>
    internal bool FtpFileMethodGiven { get; set; }

    /// <summary><see langword="true"/> when <c>--ftp-create-dirs</c> was given and no <c>--no-ftp-create-dirs</c> came after it.</summary>
    public bool FtpCreateDirectories { get; internal set; }

    /// <summary>
    /// The last <c>-P</c> / <c>--ftp-port</c> address, verbatim, for FTP active mode; <see langword="null"/>
    /// when not given or when a later <c>--ftp-pasv</c> cleared it, for passive mode (ADR-0102).
    /// </summary>
    public string? FtpPort { get; internal set; }

    /// <summary>
    /// <see langword="false"/> when the last of <c>--disable-eprt</c>, <c>--eprt</c> and their <c>--no-</c>
    /// spellings turned <c>EPRT</c> off (<c>--disable-eprt</c> or <c>--no-eprt</c>); <see langword="true"/>
    /// otherwise, as curl 8.21.0 sends <c>EPRT</c> before <c>PORT</c> by default.
    /// </summary>
    public bool FtpUseEprt { get; internal set; } = true;

    /// <summary>
    /// Whether a plaintext scheme upgrades to TLS: <see cref="TransportSecurityLevel.Required"/> when
    /// <c>--ssl-reqd</c>/<c>--ftp-ssl-reqd</c> or <c>--ftp-ssl-control</c> is in effect,
    /// <see cref="TransportSecurityLevel.Try"/> when only <c>--ssl</c>/<c>--ftp-ssl</c> is, and
    /// <see cref="TransportSecurityLevel.None"/> otherwise. curl 8.21.0 keeps the three flags apart, so
    /// <c>--no-ssl</c> does not undo <c>--ssl-reqd</c> (ADR-0102).
    /// </summary>
    public TransportSecurityLevel SslLevel =>
        SslRequired || FtpSslControlOnly ? TransportSecurityLevel.Required
        : SslTry ? TransportSecurityLevel.Try
        : TransportSecurityLevel.None;

    /// <summary>
    /// <see langword="true"/> when <c>--ftp-ssl-control</c> was given and no <c>--no-ftp-ssl-control</c> came
    /// after it: TLS is required for the control connection only, and the data connections stay clear.
    /// It outranks <c>--ssl-reqd</c> whichever comes first, as curl sets it last.
    /// </summary>
    public bool FtpSslControlOnly { get; internal set; }

    /// <summary>
    /// Whether FTPS clears the control connection with <c>CCC</c> after login:
    /// <see cref="FtpClearCommandChannel.Off"/> unless <c>--ftp-ssl-ccc</c> or <c>--ftp-ssl-ccc-mode</c> was
    /// given and no <c>--no-ftp-ssl-ccc</c> came after it, otherwise the last <c>--ftp-ssl-ccc-mode</c>'s
    /// mode, <see cref="FtpClearCommandChannel.Passive"/> when none was given, as curl 8.21.0 keeps the flag
    /// and the mode apart.
    /// </summary>
    public FtpClearCommandChannel FtpClearCommandChannel =>
        FtpSslCccRequested ? FtpSslCccMode : FtpClearCommandChannel.Off;

    /// <summary><see langword="true"/> when <c>--ftp-ssl-ccc</c> or <c>--ftp-ssl-ccc-mode</c> was given and no <c>--no-ftp-ssl-ccc</c> came after it.</summary>
    internal bool FtpSslCccRequested { get; set; }

    /// <summary>The last <c>--ftp-ssl-ccc-mode</c>'s mode; <see cref="FtpClearCommandChannel.Passive"/> when none was given.</summary>
    internal FtpClearCommandChannel FtpSslCccMode { get; set; } = FtpClearCommandChannel.Passive;

    /// <summary>
    /// The last <c>--ftp-account</c> value, verbatim: the account sent with <c>ACCT</c> when the server asks
    /// for one after the password; <see langword="null"/> when not given. An empty value is refused as blank,
    /// as curl 8.21.0 refuses it.
    /// </summary>
    public string? FtpAccount { get; internal set; }

    /// <summary>
    /// The last <c>--ftp-alternative-to-user</c> value, verbatim: the command sent when the server refuses
    /// <c>USER</c>; <see langword="null"/> when not given. An empty value is refused as blank, as curl 8.21.0
    /// refuses it.
    /// </summary>
    public string? FtpAlternativeToUser { get; internal set; }

    /// <summary><see langword="true"/> when <c>--ftp-pret</c> was given and no <c>--no-ftp-pret</c> came after it: send <c>PRET</c> before <c>EPSV</c> or <c>PASV</c>.</summary>
    public bool FtpSendPret { get; internal set; }

    /// <summary><see langword="true"/> when <c>--ssl</c> or <c>--ftp-ssl</c> was given and no <c>--no-ssl</c> or <c>--no-ftp-ssl</c> came after it.</summary>
    internal bool SslTry { get; private set; }

    /// <summary><see langword="true"/> when <c>--ssl-reqd</c> or <c>--ftp-ssl-reqd</c> was given and no <c>--no-</c> spelling of either came after it.</summary>
    internal bool SslRequired { get; set; }

    /// <summary><see langword="true"/> when <c>-l</c> / <c>--list-only</c> was given and no <c>--no-list-only</c> came after it.</summary>
    public bool ListOnly { get; internal set; }

    /// <summary><see langword="true"/> when <c>-B</c> / <c>--use-ascii</c> was given and no <c>--no-use-ascii</c> came after it: transfer as ASCII text (FTP <c>TYPE A</c>, LDAP text output).</summary>
    public bool UseAscii { get; internal set; }

    /// <summary><see langword="true"/> when <c>--crlf</c> was given and no <c>--no-crlf</c> came after it: convert LF to CRLF in an upload.</summary>
    public bool ConvertLineEndings { get; internal set; }

    /// <summary><see langword="true"/> when <c>-a</c> / <c>--append</c> was given and no <c>--no-append</c> came after it: append to the remote file instead of overwriting it.</summary>
    public bool Append { get; internal set; }

    /// <summary>
    /// Every <c>-Q</c> / <c>--quote</c> value, verbatim (prefix included, possibly empty) and in command-line order.
    /// </summary>
    public IReadOnlyList<string> QuoteCommands => quoteCommands;
    /// <summary>
    /// The <c>--create-file-mode</c> value, read as octal and at most <c>0777</c>;
    /// <see langword="null"/> when not given, where curl's default of <c>0644</c> applies.
    /// When given more than once the last value wins.
    /// </summary>
    public UnixFileMode? CreateFileMode { get; internal set; }

    /// <summary><see langword="true"/> when <c>-k</c> / <c>--insecure</c> was given and no <c>--no-insecure</c> came after it: skip server certificate verification.</summary>
    public bool Insecure { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--ssl-no-revoke</c> was given and no <c>--no-ssl-no-revoke</c> came after it:
    /// the Schannel build skips the certificate revocation check. curl accepts it in every build; the OpenSSL build ignores it.
    /// </summary>
    public bool SkipRevocationCheck { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--ssl-revoke-best-effort</c> was given and no <c>--no-ssl-revoke-best-effort</c>
    /// came after it: the Schannel build ignores a revocation check that fails for a missing or offline
    /// distribution point. curl accepts it in every build; the OpenSSL build ignores it.
    /// </summary>
    public bool RevocationCheckBestEffort { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--ssl-allow-beast</c> was given and no <c>--no-ssl-allow-beast</c> came after it:
    /// leave the TLS 1.0 BEAST workaround (record splitting) off, for servers that cannot handle it.
    /// <c>SslStream</c> has no control for it, so a connection whose range reaches TLS 1.0 runs on the hand-built
    /// TLS client, which then writes each TLS 1.0 CBC record whole (ADR-0151, BL-713).
    /// </summary>
    public bool AllowBeast { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--ca-native</c> was given and no <c>--no-ca-native</c> came after it:
    /// verify the server against the operating system's certificate store. curl accepts it in every build;
    /// the Schannel build always uses that store.
    /// </summary>
    public bool UseNativeCaStore { get; internal set; }

    /// <summary>
    /// <see langword="false"/> when the last of <c>--alpn</c> and <c>--no-alpn</c> was <c>--no-alpn</c>: offer no
    /// protocols through ALPN in the TLS handshake. <see langword="true"/> otherwise, as curl offers ALPN by default.
    /// </summary>
    public bool UseAlpn { get; internal set; } = true;

    /// <summary>
    /// <see langword="false"/> when the last of <c>--sessionid</c> and <c>--no-sessionid</c> was <c>--no-sessionid</c>:
    /// never resume a cached TLS session. <see langword="true"/> otherwise, as curl caches session IDs by default.
    /// <c>SslStream</c> cannot stop the system's session cache, so with it every connection runs on the hand-built
    /// TLS client, which neither offers nor keeps a session (ADR-0151, BL-713).
    /// </summary>
    public bool ReuseSessionIds { get; internal set; } = true;

    /// <summary>
    /// <see langword="false"/> when the last of <c>--tcp-nodelay</c> and <c>--no-tcp-nodelay</c> was
    /// <c>--no-tcp-nodelay</c>: leave Nagle's algorithm on. <see langword="true"/> otherwise, as curl sets
    /// <c>TCP_NODELAY</c> by default.
    /// </summary>
    public bool TcpNoDelay { get; internal set; } = true;

    /// <summary>
    /// <see langword="false"/> when the last of <c>--keepalive</c> and <c>--no-keepalive</c> was <c>--no-keepalive</c>:
    /// send no TCP keepalive probes. <see langword="true"/> otherwise, as curl turns keepalive on by default.
    /// </summary>
    public bool TcpKeepAlive { get; internal set; } = true;

    /// <summary>
    /// The last <c>--keepalive-time</c>: the idle seconds before the first TCP keepalive probe and between
    /// probes. Zero, the default and what <c>--keepalive-time 0</c> gives, means curl sets no time of its own,
    /// so libcurl's 60 seconds apply.
    /// </summary>
    public long TcpKeepAliveSeconds { get; internal set; }

    /// <summary>
    /// The last <c>--keepalive-cnt</c>: how many unanswered TCP keepalive probes end the connection. Zero, the
    /// default and what <c>--keepalive-cnt 0</c> gives, means curl sets no count of its own, so libcurl's 9
    /// apply.
    /// </summary>
    public long TcpKeepAliveProbeCount { get; internal set; }

    /// <summary>
    /// The last <c>--ip-tos</c>: the IPv4 Type of Service or IPv6 Traffic Class byte, 0 to 255, from a
    /// name such as <c>CS1</c> or a number. Zero, the default, sets nothing, as curl passes libcurl only a
    /// value above 0.
    /// </summary>
    public int IpTypeOfService { get; internal set; }

    /// <summary>
    /// The last <c>--vlan-priority</c>: the socket priority, 0 to 7, that Linux maps to the VLAN priority.
    /// Zero, the default, sets nothing, as curl passes libcurl only a value above 0.
    /// </summary>
    public int VlanPriority { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>--tcp-fastopen</c> and <c>--no-tcp-fastopen</c> was
    /// <c>--tcp-fastopen</c>: ask the operating system for TCP Fast Open on every TCP connection.
    /// <see langword="false"/> otherwise, curl's default.
    /// </summary>
    public bool TcpFastOpen { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>--mptcp</c> and <c>--no-mptcp</c> was <c>--mptcp</c>: open every
    /// TCP connection's socket as Multipath TCP. <see langword="false"/> otherwise, curl's default.
    /// </summary>
    public bool MultipathTcp { get; internal set; }

    /// <summary>
    /// <see langword="false"/> when the last of <c>--styled-output</c> and <c>--no-styled-output</c> was
    /// <c>--no-styled-output</c>: never style header output. <see langword="true"/> otherwise, as curl styles
    /// headers written to a terminal by default. Parsed only until BL-736 styles header output.
    /// </summary>
    public bool StyledOutput { get => globals.StyledOutput; internal set => globals.StyledOutput = value; }

    /// <summary>
    /// The <c>--cacert</c> file, verbatim; <see langword="null"/> when not given. The parser has
    /// already refused a value at which nothing exists, and records a directory here unchanged.
    /// The last value wins.
    /// </summary>
    public string? CaCertificateFile { get; internal set; }

    /// <summary>The <c>--capath</c> directory, verbatim and unchecked; <see langword="null"/> when not given. The last value wins.</summary>
    public string? CaCertificateDirectory { get; internal set; }

    /// <summary>
    /// The <c>--crlfile</c> certificate revocation list file, verbatim, checked as <c>--cacert</c> is;
    /// <see langword="null"/> when not given. The last value wins. Applied by BL-608 to BL-610.
    /// </summary>
    public string? CertificateRevocationListFile { get; internal set; }

    /// <summary>
    /// The <c>--pinnedpubkey</c> value, verbatim and unchecked: a public key file, or <c>sha256//</c> hashes
    /// separated by <c>;</c>, for the connector to match the server's key against. <see langword="null"/>
    /// when not given. The last value wins. Applied by BL-608 to BL-610.
    /// </summary>
    public string? PinnedPublicKey { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--cert-status</c> was given and no <c>--no-cert-status</c> came after it:
    /// require a good OCSP response stapled to the server certificate. Applied by BL-608 to BL-610.
    /// </summary>
    public bool RequireCertificateStatus { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--ssl-auto-client-cert</c> was given and no <c>--no-ssl-auto-client-cert</c>
    /// came after it: the Schannel build picks a client certificate from the user's store by itself.
    /// curl accepts it in every build; the OpenSSL build ignores it. Applied by BL-608 to BL-610.
    /// </summary>
    public bool AutoClientCertificate { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--proxy-insecure</c> was given and no <c>--no-proxy-insecure</c> came
    /// after it: skip verification of an HTTPS proxy's certificate. <c>-k</c> never reaches the proxy.
    /// </summary>
    public bool ProxyInsecure { get; internal set; }

    /// <summary>
    /// The <c>--proxy-cacert</c> file, verbatim, checked as <c>--cacert</c> is; <see langword="null"/> when
    /// not given. It verifies an HTTPS proxy only, and <c>--cacert</c> never does. The last value wins.
    /// </summary>
    public string? ProxyCaCertificateFile { get; internal set; }

    /// <summary>The <c>--proxy-capath</c> directory, verbatim and unchecked; <see langword="null"/> when not given. The last value wins.</summary>
    public string? ProxyCaCertificateDirectory { get; internal set; }

    /// <summary>
    /// The <c>--proxy-cert</c> value, verbatim, with <c>certificate[:password]</c> not yet split, as
    /// <see cref="ClientCertificate"/> is; it authenticates to an HTTPS proxy only. <see langword="null"/>
    /// when not given. The last value wins. Applied by BL-606 and BL-611.
    /// </summary>
    public string? ProxyClientCertificate { get; internal set; }

    /// <summary>The <c>--proxy-key</c> private key file, verbatim and unchecked; <see langword="null"/> when not given. The last value wins.</summary>
    public string? ProxyPrivateKey { get; internal set; }

    /// <summary>The <c>--proxy-cert-type</c> value, verbatim, as <see cref="ClientCertificateType"/> is; <see langword="null"/> when not given. The last value wins.</summary>
    public string? ProxyClientCertificateType { get; internal set; }

    /// <summary>The <c>--proxy-key-type</c> value, verbatim, as <see cref="PrivateKeyType"/> is; <see langword="null"/> when not given. The last value wins.</summary>
    public string? ProxyPrivateKeyType { get; internal set; }

    /// <summary>The <c>--proxy-pass</c> passphrase for the proxy's private key, verbatim; <see langword="null"/> when not given. The last value wins.</summary>
    public string? ProxyPassphrase { get; internal set; }

    /// <summary>The <c>--proxy-ciphers</c> list, verbatim; <see langword="null"/> when not given. The last value wins.</summary>
    public string? ProxyCiphers { get; internal set; }

    /// <summary>The <c>--proxy-tls13-ciphers</c> list, verbatim; <see langword="null"/> when not given. The last value wins.</summary>
    public string? ProxyTls13Ciphers { get; internal set; }

    /// <summary>
    /// The <c>--proxy-crlfile</c> certificate revocation list file, verbatim, checked as <c>--crlfile</c> is;
    /// <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? ProxyCertificateRevocationListFile { get; internal set; }

    /// <summary>
    /// The <c>--proxy-pinnedpubkey</c> value, verbatim and unchecked, as <see cref="PinnedPublicKey"/> is, matched
    /// against the HTTPS proxy's key; <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? ProxyPinnedPublicKey { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--proxy-ca-native</c> was given and no <c>--no-proxy-ca-native</c> came
    /// after it: verify an HTTPS proxy against the operating system's certificate store.
    /// </summary>
    public bool ProxyUseNativeCaStore { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--proxy-ssl-auto-client-cert</c> was given and no
    /// <c>--no-proxy-ssl-auto-client-cert</c> came after it: the Schannel build picks a client certificate for
    /// the HTTPS proxy from the user's store by itself.
    /// </summary>
    public bool ProxyAutoClientCertificate { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--proxy-ssl-allow-beast</c> was given and no
    /// <c>--no-proxy-ssl-allow-beast</c> came after it: leave the TLS 1.0 BEAST workaround off towards the
    /// HTTPS proxy, as <see cref="AllowBeast"/> does towards the server.
    /// </summary>
    public bool ProxyAllowBeast { get; internal set; }

    /// <summary>
    /// The <c>--proxy-tlsuser</c> TLS-SRP user name for the HTTPS proxy, verbatim, empty included, as curl 8.18.0
    /// accepts it (measured, BL-1135), unlike <c>--tlsuser</c>; <see langword="null"/> when not given.
    /// </summary>
    public string? ProxyTlsUser { get; internal set; }

    /// <summary>
    /// The <c>--proxy-tlspassword</c> TLS-SRP password for the HTTPS proxy, verbatim; <see langword="null"/> when
    /// not given. An empty value is refused as blank, as curl 8.18.0 refuses it (measured, BL-1135), unlike
    /// <c>--tlspassword</c>.
    /// </summary>
    public string? ProxyTlsPassword { get; internal set; }

    /// <summary>
    /// The <c>--proxy-tlsauthtype</c> value: <c>SRP</c>, the only type curl accepts (case-sensitively), or
    /// <see langword="null"/> when not given.
    /// </summary>
    public string? ProxyTlsAuthType { get; internal set; }

    /// <summary>
    /// The <c>-E</c> / <c>--cert</c> value, verbatim, with <c>certificate[:password]</c> not yet split;
    /// <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? ClientCertificate { get; internal set; }

    /// <summary>The <c>--key</c> private key file, verbatim and unchecked; <see langword="null"/> when not given. The last value wins.</summary>
    public string? PrivateKey { get; internal set; }

    /// <summary>
    /// The <c>--cert-type</c> value (for example <c>PEM</c>, <c>DER</c> or <c>P12</c>), verbatim; the TLS layer
    /// compares it case-insensitively. <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? ClientCertificateType { get; internal set; }

    /// <summary>
    /// The <c>--key-type</c> value (for example <c>PEM</c> or <c>DER</c>), verbatim; the TLS layer compares it
    /// case-insensitively. <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? PrivateKeyType { get; internal set; }

    /// <summary>
    /// The <c>--pass</c> passphrase for the private key, verbatim; <see langword="null"/> when not given.
    /// The last value wins.
    /// </summary>
    public string? Passphrase { get; internal set; }

    /// <summary>
    /// The <c>--pubkey</c> SSH public key file, verbatim and unchecked; <see langword="null"/> when not given.
    /// The last value wins. The SSH private key and its passphrase are <see cref="PrivateKey"/>,
    /// <see cref="PrivateKeyType"/> and <see cref="Passphrase"/>, which <c>--key</c>, <c>--key-type</c> and
    /// <c>--pass</c> set for TLS and SSH alike.
    /// </summary>
    public string? SshPublicKeyFile { get; internal set; }

    /// <summary>
    /// The <c>--knownhosts</c> file, which curl 8.21.0 requires to exist (a directory passes) when the option is
    /// read; <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? SshKnownHostsFile { get; internal set; }

    /// <summary>
    /// The <c>--hostpubmd5</c> value, verbatim: exactly 32 characters, which curl 8.21.0 checks when the option is
    /// read without checking that they are hex digits; <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? SshHostPublicKeyMd5 { get; internal set; }

    /// <summary>
    /// The <c>--hostpubsha256</c> value, verbatim and unchecked (curl 8.21.0 accepts <c>!!!</c> when the option is
    /// read); <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? SshHostPublicKeySha256 { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--compressed-ssh</c> asks for SSH compression; <c>--no-compressed-ssh</c>
    /// clears it. <see langword="false"/> by default.
    /// </summary>
    public bool SshCompression { get; internal set; }

    /// <summary>
    /// The lowest TLS version to accept: <see cref="ObsoleteTlsProtocols.Tls10"/> for <c>-1</c>/<c>--tlsv1</c>
    /// and <c>--tlsv1.0</c> (1.0 or later), <see cref="ObsoleteTlsProtocols.Tls11"/> for <c>--tlsv1.1</c>,
    /// <see cref="SslProtocols.Tls12"/> for <c>--tlsv1.2</c>, <see cref="SslProtocols.Tls13"/> for
    /// <c>--tlsv1.3</c>; <see langword="null"/> when none was given. The last one given wins, as in curl 8.21.0.
    /// </summary>
    public SslProtocols? MinimumTlsVersion { get; internal set; }

    /// <summary>
    /// The highest TLS version to offer, from <c>--tls-max</c>: <see cref="ObsoleteTlsProtocols.Tls10"/> for
    /// <c>1.0</c>, <see cref="ObsoleteTlsProtocols.Tls11"/> for <c>1.1</c>, <see cref="SslProtocols.Tls12"/> for
    /// <c>1.2</c>, <see cref="SslProtocols.Tls13"/> for <c>1.3</c>; <see langword="null"/> when not given or given
    /// as <c>default</c>. curl 8.21.0 applies it to the origin only, never to an HTTPS proxy (measured, BL-502).
    /// The last value wins. A ceiling below <see cref="MinimumTlsVersion"/>, in either order, is refused at parse
    /// time, as curl refuses it.
    /// </summary>
    public SslProtocols? MaximumTlsVersion { get; internal set; }

    /// <summary>
    /// The lowest TLS version to accept from an HTTPS proxy: <see cref="ObsoleteTlsProtocols.Tls10"/> (1.0 or
    /// later) for <c>--proxy-tlsv1</c>; <see langword="null"/> when not given.
    /// </summary>
    public SslProtocols? ProxyMinimumTlsVersion { get; internal set; }

    /// <summary>
    /// The schemes <c>--proto</c> allows, lowercase, read by <see cref="CommandLineProtocolSet"/>;
    /// <see langword="null"/> when not given, which allows every scheme. Each <c>--proto</c> starts again from
    /// every scheme curl knows, so the last one given decides (curl 8.21.0, measured 2026-09-28).
    /// </summary>
    public IReadOnlySet<string>? AllowedProtocols { get; internal set; }

    /// <summary>
    /// The schemes <c>--proto-redir</c> allows a followed redirect to use, lowercase, read as
    /// <see cref="AllowedProtocols"/> is; <see langword="null"/> when not given, which leaves curl's default.
    /// </summary>
    public IReadOnlySet<string>? AllowedRedirectProtocols { get; internal set; }

    /// <summary>
    /// The scheme <c>--proto-default</c> names for a URL given without one, lowercase; <see langword="null"/>
    /// when not given. The last value wins.
    /// </summary>
    public string? DefaultProtocol { get; internal set; }

    /// <summary>The last <c>--proto-default</c> value as typed, case kept, which <c>--libcurl</c> writes as <c>CURLOPT_DEFAULT_PROTOCOL</c>; <see langword="null"/> when not given.</summary>
    internal string? DefaultProtocolAsTyped { get; set; }

    /// <summary>The <c>--ciphers</c> list, verbatim; <see langword="null"/> when not given. The last value wins.</summary>
    public string? Ciphers { get; internal set; }

    /// <summary>The <c>--tls13-ciphers</c> list, verbatim; <see langword="null"/> when not given. The last value wins.</summary>
    public string? Tls13Ciphers { get; internal set; }

    /// <summary>
    /// The <c>--curves</c> list of key-exchange groups, verbatim; <see langword="null"/> when not given. An empty
    /// value is refused as blank, as curl 8.21.0 refuses it. The last value wins.
    /// </summary>
    public string? Curves { get; internal set; }

    /// <summary>
    /// The <c>--sigalgs</c> list of signature algorithms, verbatim; <see langword="null"/> when not given. An
    /// empty value is refused as blank, as curl 8.21.0 refuses it. The last value wins.
    /// </summary>
    public string? SignatureAlgorithms { get; internal set; }

    /// <summary><see langword="true"/> when <c>--tls-earlydata</c> was given and no <c>--no-tls-earlydata</c> came after it: send TLS 1.3 early data on a resumed session.</summary>
    public bool TlsEarlyData { get; internal set; }

    /// <summary>
    /// The last <c>--ech</c> value that is neither <c>pn:&lt;name&gt;</c> nor <c>ecl:&lt;list&gt;</c>, verbatim:
    /// the mode, <c>false</c>, <c>grease</c>, <c>true</c> or <c>hard</c>; <see langword="null"/> when none was
    /// given. curl 8.21.0 does not check the keyword while parsing, so neither does this.
    /// </summary>
    public string? Ech { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <see cref="Ech"/> is a value libcurl's <c>setopt_ech</c> refuses when curl sets
    /// it: anything but <c>false</c>, <c>grease</c>, <c>true</c> or <c>hard</c>, compared case-sensitively, a
    /// value longer than four characters starting <c>ecl:</c> or one longer than three starting <c>pn:</c>
    /// (curl 8.21.0, <c>lib/setopt.c</c>). curl then fails the transfer with exit 43 and
    /// <c>curl: (43) setopt 0x2855 got bad argument</c> (measured 2026-10-02, BL-1107; ADR-0378).
    /// </summary>
    public bool EchModeIsMalformed => Ech is { } mode && !LibcurlAcceptsEchMode(mode);

    /// <summary>
    /// The public name of the last <c>--ech pn:&lt;name&gt;</c>, without its <c>pn:</c> prefix;
    /// <see langword="null"/> when none was given.
    /// </summary>
    public string? EchPublicName { get; internal set; }

    /// <summary>
    /// The base64 ECHConfigList of the last <c>--ech ecl:&lt;list&gt;</c>, without its <c>ecl:</c> prefix, or the
    /// text of the file <c>ecl:@&lt;file&gt;</c> names (standard input for <c>ecl:@-</c>) with its carriage
    /// returns and line feeds removed; <see langword="null"/> when none was given.
    /// </summary>
    public string? EchConfigList { get; internal set; }

    /// <summary>
    /// The <c>--engine</c> name, verbatim; <see langword="null"/> when not given. <c>--engine list</c> also sets
    /// <see cref="EngineListRequested"/>. An empty value is refused as blank, as curl 8.21.0 refuses it.
    /// </summary>
    public string? Engine { get; internal set; }

    /// <summary>The <c>--tlsuser</c> TLS-SRP user name, verbatim; <see langword="null"/> when not given. An empty value is refused as blank, as curl 8.21.0 refuses it.</summary>
    public string? TlsUser { get; internal set; }

    /// <summary>The <c>--tlspassword</c> TLS-SRP password, verbatim, empty included, as curl 8.21.0 accepts it; <see langword="null"/> when not given.</summary>
    public string? TlsPassword { get; internal set; }

    /// <summary>
    /// The <c>--tlsauthtype</c> value: <c>SRP</c>, the only type curl 8.21.0 accepts (case-sensitively), or
    /// <see langword="null"/> when not given.
    /// </summary>
    public string? TlsAuthType { get; internal set; }

    /// <summary>
    /// The <c>--ssl-sessions</c> file TLS session tickets are loaded from before the transfers and saved to
    /// after them; <see langword="null"/> when not given. Global, as curl 8.21.0 keeps it: every option group
    /// shares the last value given.
    /// </summary>
    public string? SslSessionsFile { get => globals.SslSessionsFile; internal set => globals.SslSessionsFile = value; }

    /// <summary>
    /// The <c>--libcurl</c> file the C source for the command line is written to once the transfers are
    /// done (<see cref="LibcurlSourceCode" />), <c>-</c> for standard output; <see langword="null" /> when not
    /// given. Global, as curl 8.21.0 keeps it: every option group shares the last value given.
    /// </summary>
    public string? LibcurlFile { get => globals.LibcurlFile; internal set => globals.LibcurlFile = value; }

    /// <summary>
    /// The <c>-r</c> / <c>--range</c> text as curl keeps it, not yet parsed; <see langword="null"/>
    /// when not given. A value that starts with a digit and has no dash is kept as that leading
    /// number with a dash appended (<c>5abc</c> becomes <c>5-</c>); anything else is kept verbatim.
    /// <c>ByteRangeParser</c> in <c>Curl.Core.UnitLibrary</c> turns it into the range a handler
    /// receives. The last value wins.
    /// </summary>
    public string? Range { get; internal set; }

    /// <summary>
    /// The <c>-C</c> / <c>--continue-at</c> byte offset; <see langword="null"/> when not given, or
    /// when <c>-C -</c> asked for the offset to be worked out (<see cref="ResumeFromOutputSize"/>).
    /// The last value wins.
    /// </summary>
    public long? ResumeFrom { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last <c>-C</c> / <c>--continue-at</c> was <c>-</c>: resume
    /// from the size of the output file.
    /// </summary>
    public bool ResumeFromOutputSize { get; internal set; }

    /// <summary>
    /// The <c>--max-filesize</c> limit in bytes, units and fractions already applied;
    /// <see langword="null"/> when not given. Zero is recorded as given and means no limit, as it
    /// does to curl. The last value wins.
    /// </summary>
    public long? MaxFileSize { get; internal set; }

    /// <summary>
    /// The <c>--connect-timeout</c> limit, to the millisecond, at most about 29,000 years (see
    /// <see cref="CommandLineNumber.ParseSeconds"/>); <see langword="null"/> when not given.
    /// Past about 49.7 days it is longer than a .NET timer accepts, so cap it before waiting on
    /// it. Zero is recorded as given and means no limit, as it does to curl. The last value wins.
    /// </summary>
    public TimeSpan? ConnectTimeout { get; internal set; }

    /// <summary>
    /// The last <c>--happy-eyeballs-timeout-ms</c>: how long the connect waits on the first address
    /// family before it also dials the other, in whole milliseconds (see
    /// <see cref="CommandLineNumber.ParseMilliseconds"/>); <see langword="null"/> when not given, for
    /// curl's 200. Zero dials both families at once, as it does to curl.
    /// </summary>
    public TimeSpan? HappyEyeballsTimeout { get; internal set; }

    /// <summary>
    /// The <c>-m</c> / <c>--max-time</c> limit on the whole transfer, to the millisecond, at most
    /// about 29,000 years (see <see cref="CommandLineNumber.ParseSeconds"/>); <see langword="null"/>
    /// when not given. Past about 49.7 days it is longer than a .NET timer accepts, so cap it
    /// before waiting on it. Zero is recorded as given and means no limit, as it
    /// does to curl. The last value wins.
    /// </summary>
    public TimeSpan? MaxTime { get; internal set; }

    /// <summary>
    /// The <c>--expect100-timeout</c> wait for <c>100 Continue</c> before a request body is sent
    /// anyway, to the millisecond, read as <c>--connect-timeout</c> is (see
    /// <see cref="CommandLineNumber.ParseSeconds"/>); <see langword="null"/> when not given.
    /// Zero is recorded as given and means curl's default one second, as it does to curl
    /// 8.21.0 (measured, BL-624). The last value wins.
    /// </summary>
    public TimeSpan? Expect100Timeout { get; internal set; }

    /// <summary>
    /// The <c>--retry</c> count: how many times a transient failure is retried, from 0 to the
    /// platform's C <c>LONG_MAX</c> (<see cref="CommandLineNumber.PlatformLongMaximum"/>). Zero,
    /// the default, retries nothing. The last value wins.
    /// </summary>
    public long RetryCount { get; internal set; }

    /// <summary>
    /// The <c>--retry-delay</c> wait between retries, to the millisecond, read as
    /// <see cref="CommandLineNumber.ParseSeconds"/> reads <c>-m</c>; <see langword="null"/> when not
    /// given, which leaves curl's own backoff in place. The last value wins.
    /// </summary>
    public TimeSpan? RetryDelay { get; internal set; }

    /// <summary>
    /// The <c>--retry-max-time</c> limit on the time spent retrying, to the millisecond, read as
    /// <see cref="CommandLineNumber.ParseSeconds"/> reads <c>-m</c>; <see langword="null"/> when not
    /// given. Zero is recorded as given and means no limit, as it does to curl. The last value wins.
    /// </summary>
    public TimeSpan? RetryMaxTime { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--retry-all-errors</c> was given and no
    /// <c>--no-retry-all-errors</c> came after it: <c>--retry</c> retries after any error.
    /// </summary>
    public bool RetryAllErrors { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--retry-connrefused</c> was given and no
    /// <c>--no-retry-connrefused</c> came after it: <c>--retry</c> counts a refused connection as
    /// transient.
    /// </summary>
    public bool RetryConnectionRefused { get; internal set; }

    /// <summary>
    /// The <c>--limit-rate</c> ceiling in bytes per second, for both download and upload, read as
    /// <see cref="CommandLineNumber.ParseSize"/> reads <c>--max-filesize</c> (<c>b</c>, <c>k</c>,
    /// <c>m</c>, <c>g</c>, <c>t</c> and <c>p</c> in either case, and fractions);
    /// <see langword="null"/> when not given. Zero is recorded as given and means no limit, as it
    /// does to curl. The last value wins.
    /// </summary>
    public long? LimitRate { get; internal set; }

    /// <summary>
    /// The <c>-Y</c> / <c>--speed-limit</c> in bytes per second below which a transfer is too slow;
    /// <see langword="null"/> when not given. Curl 8.21.0 aborts a transfer that stays slower than
    /// this for <see cref="SpeedTimeSeconds"/>, or for 30 seconds when that is not given (measured
    /// through <c>--libcurl</c>). The last value wins, except that a <c>-y</c> given after a
    /// zero <c>-Y</c> makes it 1, as curl 8.21.0's tool does when it parses <c>-y</c> (ADR-0115).
    /// </summary>
    public long? SpeedLimit { get; internal set; }

    /// <summary>
    /// The <c>-y</c> / <c>--speed-time</c> in whole seconds a transfer may stay slower than
    /// <see cref="SpeedLimit"/>; <see langword="null"/> when not given. When it is given and
    /// <see cref="SpeedLimit"/> is not, curl 8.21.0 uses a limit of 1 byte per second (measured
    /// through <c>--libcurl</c>). The last value wins, except that a <c>-Y</c> given after a
    /// zero <c>-y</c> makes it 30, as curl 8.21.0's tool does when it parses <c>-Y</c> (ADR-0115).
    /// </summary>
    public long? SpeedTimeSeconds { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-R</c> / <c>--remote-time</c> was given and no
    /// <c>--no-remote-time</c> came after it: give the output file the remote file's time.
    /// </summary>
    public bool RemoteTime { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--xattr</c> was given and no <c>--no-xattr</c> came after it:
    /// store the transfer's URL, <c>Referer</c> and content type as extended attributes of the output
    /// file, on the operating systems where curl does (ADR-0320).
    /// </summary>
    public bool ExtendedAttributes { get; internal set; }

    /// <summary>
    /// The <c>-z</c> / <c>--time-cond</c> condition: the date read by <see cref="CurlDateParser"/> and
    /// its direction; <see langword="null"/> when not given, or when the last value was not a date,
    /// which curl 8.21.0 warns about and then transfers unconditionally. The last value wins.
    /// </summary>
    public TimeCondition? TimeCondition { get; internal set; }

    /// <summary>
    /// The <c>-X</c> / <c>--request</c> method, verbatim and never empty; <see langword="null"/> when
    /// not given. The last value wins.
    /// </summary>
    public string? RequestMethod { get; internal set; }

    /// <summary>
    /// The <c>-H</c> / <c>--header</c> values in command-line order, each verbatim, empty included,
    /// with an <c>@file</c> value replaced by the file's non-empty lines in file order.
    /// </summary>
    public IReadOnlyList<string> Headers => headers;

    /// <summary>
    /// The <c>--proxy-header</c> values in command-line order, each verbatim, empty included,
    /// with an <c>@file</c> value replaced by the file's non-empty lines in file order.
    /// </summary>
    public IReadOnlyList<string> ProxyHeaders => proxyHeaders;

    /// <summary>
    /// The multipart form <c>-F</c> / <c>--form</c> and <c>--form-string</c> values describe, one
    /// top-level part per value in command-line order, parts given between <c>name=(</c> and <c>=)</c>
    /// inside the part that opened them; empty when neither option was given. A multipart part still
    /// open when the command line ends is simply closed there.
    /// </summary>
    public IReadOnlyList<FormPartSpecification> FormParts => formParts;

    /// <summary>
    /// <see langword="true"/> when the last of <c>--form-escape</c> and <c>--no-form-escape</c> was
    /// <c>--form-escape</c>: <see cref="FormParts"/> names and file names are escaped with backslashes
    /// (<c>\\</c>, <c>\"</c>) rather than curl 8.21.0's default <c>%22</c>, <c>%0D</c> and <c>%0A</c> (BL-625).
    /// </summary>
    public bool FormEscape { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>--disallow-username-in-url</c> and
    /// <c>--no-disallow-username-in-url</c> was <c>--disallow-username-in-url</c>: a URL with user
    /// information - an <c>@</c> in its authority, even with an empty user - is refused with exit 67
    /// before any connection, the first URL and every followed redirect target alike (BL-626).
    /// </summary>
    public bool DisallowUsernameInUrl { get; internal set; }

    /// <summary>
    /// The <c>-A</c> / <c>--user-agent</c> value, verbatim; empty when given empty, which curl 8.21.0
    /// sends as no <c>User-Agent</c> header at all; <see langword="null"/> when not given. The last value wins.
    /// </summary>
    public string? UserAgent { get; internal set; }

    /// <summary>
    /// The <c>-e</c> / <c>--referer</c> value, verbatim, empty included, with a trailing <c>;auto</c>
    /// removed; <see langword="null"/> when not given, or when the value was <c>;auto</c> alone. The
    /// last value wins.
    /// </summary>
    public string? Referer { get; internal set; }

    /// <summary>
    /// Whether the last <c>-e</c> / <c>--referer</c> value ended in <c>;auto</c>: when on, a followed
    /// redirect sends the previous URL as <c>Referer</c>, per curl 8.21.0. A later value without the
    /// suffix turns it off again.
    /// </summary>
    public bool AutoReferer { get; internal set; }

    /// <summary>
    /// Every <c>-b</c> / <c>--cookie</c> value, cookie strings and cookie file names alike, in
    /// command-line order. Empty is accepted, as a file name.
    /// </summary>
    public IReadOnlyList<CommandLineCookie> Cookies => cookies;

    /// <summary>
    /// The last <c>-c</c> / <c>--cookie-jar</c> value, verbatim and never empty: the file to write
    /// every cookie to after the transfer (<c>-</c> for standard output). <see langword="null"/>
    /// when not given.
    /// </summary>
    public string? CookieJar { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-j</c> / <c>--junk-session-cookies</c> was given and no
    /// <c>--no-junk-session-cookies</c> came after it: drop the session cookies read from a
    /// <c>-b</c> file.
    /// </summary>
    public bool JunkSessionCookies { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-L</c> / <c>--location</c>, <c>--location-trusted</c> or
    /// <c>--follow</c> was given and no <c>--no-location</c>, <c>--no-location-trusted</c> or
    /// <c>--no-follow</c> came after it: follow redirects.
    /// </summary>
    public bool FollowRedirects { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--follow</c> was the last of <c>-L</c>, <c>--location-trusted</c>,
    /// <c>--follow</c> and their <c>--no-</c> spellings given: redirects are followed with the method
    /// changed as the HTTP specification says, a <c>-X</c> method dropped whenever a redirect switches
    /// the request to GET, rather than kept as <c>-L</c> keeps it (curl 8.21.0, measured, BL-627 Notes).
    /// </summary>
    public bool FollowRedirectsPerSpec { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--location-trusted</c> was given and no <c>--no-location-trusted</c>
    /// came after it: send the <c>-u</c> credentials and any <c>Authorization</c> header to every host
    /// a redirect leads to, not only the first. <c>-L</c> and <c>--no-location</c> leave it as it is,
    /// as in curl 8.21.0.
    /// </summary>
    public bool SendCredentialsToRedirectHosts { get; internal set; }

    /// <summary>
    /// The <c>--max-redirs</c> limit on redirects followed: 50 when not given, as in curl 8.21.0,
    /// and <c>-1</c> for no limit. A limit past <see cref="int.MaxValue"/> (accepted where a C
    /// <c>long</c> is 64 bits) is recorded as <see cref="int.MaxValue"/>, which no transfer reaches
    /// either. The last value wins.
    /// </summary>
    public int MaxRedirects { get; internal set; } = 50;

    /// <summary><see langword="true"/> when <c>--post301</c> was given and no <c>--no-post301</c> came after it: keep a POST a POST after a 301.</summary>
    public bool KeepPostAfter301 { get; internal set; }

    /// <summary><see langword="true"/> when <c>--post302</c> was given and no <c>--no-post302</c> came after it: keep a POST a POST after a 302.</summary>
    public bool KeepPostAfter302 { get; internal set; }

    /// <summary><see langword="true"/> when <c>--post303</c> was given and no <c>--no-post303</c> came after it: keep a POST a POST after a 303.</summary>
    public bool KeepPostAfter303 { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when the last of <c>-i</c> / <c>--show-headers</c> / <c>--include</c>,
    /// <c>-I</c> / <c>--head</c> and their <c>--no-</c> spellings turned it on: write the response
    /// headers to the output before the body.
    /// </summary>
    public bool ShowHeaders { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-I</c> / <c>--head</c> was given and no <c>--no-head</c> came
    /// after it: ask for the headers only (a <c>HEAD</c> request over HTTP). It maps onto the
    /// transfer's <c>NoBody</c>.
    /// </summary>
    public bool NoBody { get; internal set; }

    /// <summary>
    /// How an HTTP error response ends the transfer: <see cref="HttpFailMode.Fail"/> for <c>-f</c> /
    /// <c>--fail</c>, <see cref="HttpFailMode.FailWithBody"/> for <c>--fail-with-body</c>, whichever came
    /// last; <see cref="HttpFailMode.None"/> when neither was given or <c>--no-fail</c> or
    /// <c>--no-fail-with-body</c> came after it. Either <c>--no-</c> spelling turns off both, as in
    /// curl 8.21.0.
    /// </summary>
    public HttpFailMode FailMode { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--fail-early</c> was given and no <c>--no-fail-early</c> came after
    /// it: stop at the first transfer that fails instead of going on to the next URL.
    /// </summary>
    public bool FailEarly { get => globals.FailEarly; internal set => globals.FailEarly = value; }

    /// <summary>
    /// The <c>--parallel-max</c> curl 8.21.0 uses when none is given, or when the one given is zero.
    /// </summary>
    public const int DefaultParallelMax = 50;

    /// <summary>
    /// The largest <c>--parallel-max</c> and <c>--parallel-max-host</c> curl 8.21.0 supports; a larger
    /// value is taken as this one.
    /// </summary>
    public const int LargestParallelLimit = 65535;

    /// <summary>
    /// <see langword="true"/> when <c>-Z</c> / <c>--parallel</c> was given and no <c>--no-parallel</c> came
    /// after it: run the transfers concurrently instead of one after another.
    /// </summary>
    public bool Parallel { get => globals.Parallel; internal set => globals.Parallel = value; }

    /// <summary>
    /// <see langword="true"/> when <c>--parallel-immediate</c> was given and no
    /// <c>--no-parallel-immediate</c> came after it: in a parallel run, open new connections at once
    /// rather than wait to multiplex on an existing one.
    /// </summary>
    public bool ParallelImmediate { get => globals.ParallelImmediate; internal set => globals.ParallelImmediate = value; }

    /// <summary>
    /// The most transfers a parallel run has in progress at once, from <c>--parallel-max</c>:
    /// <see cref="DefaultParallelMax"/> when it is not given or given as zero, and at most
    /// <see cref="LargestParallelLimit"/>.
    /// </summary>
    public int ParallelMax { get => globals.ParallelMax; internal set => globals.ParallelMax = value; }

    /// <summary>
    /// The most connections a parallel run has open to one protocol, host and port at once, from
    /// <c>--parallel-max-host</c>: zero, meaning no limit, when it is not given or given as zero, and at
    /// most <see cref="LargestParallelLimit"/>.
    /// </summary>
    public int ParallelMaxHost { get => globals.ParallelMaxHost; internal set => globals.ParallelMaxHost = value; }

    /// <summary>
    /// The least time in milliseconds from one serial transfer's start to the next one's, from the last <c>--rate</c> as
    /// <see cref="TransferStartRate"/> reads it: <c>--rate 2/s</c> is 500. A <see cref="long"/>, since a period of up to <see cref="long.MaxValue"/> milliseconds is accepted. <see langword="null"/> when
    /// not given. Global, so it reaches every <c>--next</c> group; a <c>-Z</c> run ignores it, as curl
    /// 8.21.0 does.
    /// </summary>
    public long? MillisecondsBetweenTransferStarts { get => globals.MillisecondsBetweenTransferStarts; internal set => globals.MillisecondsBetweenTransferStarts = value; }

    /// <summary>
    /// <see langword="true"/> when <c>--compressed</c> was given and no <c>--no-compressed</c> came after
    /// it: ask for a compressed response and decompress it.
    /// </summary>
    public bool Compressed { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--raw</c> was given and no <c>--no-raw</c> came after it: pass
    /// content and transfer encodings through undecoded.
    /// </summary>
    public bool Raw { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--tr-encoding</c> was given and no <c>--no-tr-encoding</c> came
    /// after it: ask for a compressed transfer encoding and decode it.
    /// </summary>
    public bool TransferEncoding { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--ignore-content-length</c> was given and no
    /// <c>--no-ignore-content-length</c> came after it: ignore the response's <c>Content-Length</c>.
    /// </summary>
    public bool IgnoreContentLength { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--path-as-is</c> was given and no <c>--no-path-as-is</c> came after
    /// it: send the URL path without squashing <c>/../</c> and <c>/./</c>.
    /// </summary>
    public bool PathAsIs { get; internal set; }

    /// <summary>
    /// The last <c>--request-target</c>, sent in place of the URL's path in the request line;
    /// <see langword="null"/> when not given. An empty value is refused as blank.
    /// </summary>
    public string? RequestTarget { get; internal set; }

    /// <summary>
    /// The last <c>--ipfs-gateway</c>, verbatim, for <c>Curl.Core</c>'s <c>IpfsGatewayRewriter</c>;
    /// <see langword="null"/> when not given. An empty value is refused as blank; any other value is
    /// accepted, because curl 8.21.0 checks the gateway only when it rewrites an <c>ipfs://</c> or
    /// <c>ipns://</c> URL, not while it reads the options.
    /// </summary>
    public string? IpfsGateway { get; internal set; }

    /// <summary>
    /// The last <c>--mail-from</c>, the SMTP reverse path, verbatim; <see langword="null"/> when not
    /// given. An empty value is refused as blank.
    /// </summary>
    public string? MailFrom { get; internal set; }

    /// <summary>
    /// Every <c>--mail-rcpt</c> value, verbatim and in command-line order; empty when none was given.
    /// An empty value is accepted and kept, as curl 8.21.0 accepts it.
    /// </summary>
    public IReadOnlyList<string> MailRecipients => mailRecipients;

    /// <summary>
    /// The last <c>--mail-auth</c>, the address for SMTP's <c>AUTH=</c> parameter, verbatim;
    /// <see langword="null"/> when not given. An empty value is refused as blank.
    /// </summary>
    public string? MailAuth { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--mail-rcpt-allowfails</c> was given and no
    /// <c>--no-mail-rcpt-allowfails</c> came after it.
    /// </summary>
    public bool MailRecipientAllowFails { get; internal set; }

    /// <summary>
    /// The IMAP flags an <c>APPEND</c> sets: <see cref="ImapUploadFlags.Seen"/> until an
    /// <c>--upload-flags</c> changes it. Each <c>--upload-flags</c> sets the flags it names and clears
    /// those it names with a leading <c>-</c>, left to right, on top of the flags before it.
    /// </summary>
    public ImapUploadFlags UploadFlags { get; internal set; } = ImapUploadFlags.Seen;

    /// <summary>
    /// The last <c>--login-options</c>, verbatim; <see langword="null"/> when not given. An empty value
    /// is accepted and kept, as curl 8.21.0 accepts it.
    /// </summary>
    public string? LoginOptions { get; internal set; }

    /// <summary>
    /// The last <c>--sasl-authzid</c>, the SASL authorization identity; <see langword="null"/> when
    /// not given. An empty value is refused as blank.
    /// </summary>
    public string? SaslAuthorizationIdentity { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--sasl-ir</c> was given and no <c>--no-sasl-ir</c> came after
    /// it: send the SASL initial response with the authentication command.
    /// </summary>
    public bool SaslInitialResponse { get; internal set; }

    /// <summary>
    /// The HTTP version the last <c>-0</c> / <c>--http1.0</c>, <c>--http1.1</c>, <c>--http2</c> or
    /// <c>--http2-prior-knowledge</c> asked for; <see langword="null"/> when none was given, which
    /// means curl's default: an HTTP/1.1 request line, and the platform's default ALPN offer (ADR-0141).
    /// </summary>
    public RequestedHttpVersion? HttpVersion { get; private set; }

    /// <summary>
    /// The IP address family the last <c>-4</c> / <c>--ipv4</c> or <c>-6</c> / <c>--ipv6</c> chose;
    /// <see cref="IpAddressFamilyChoice.Either"/> when neither was given. A later one overrides an
    /// earlier one without a warning, as curl 8.21.0 does.
    /// </summary>
    public IpAddressFamilyChoice IpAddressFamily { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>--http0.9</c> was given and no <c>--no-http0.9</c> came after it:
    /// accept an HTTP/0.9 reply, one with no status line, instead of refusing it.
    /// </summary>
    public bool AllowHttp09Reply { get; internal set; }

    /// <summary>
    /// The HTTP request method <c>-I</c> / <c>--head</c> (<see cref="SelectedHttpMethod.Head"/>),
    /// <c>--no-head</c> (<see cref="SelectedHttpMethod.Get"/>) or <c>-F</c> / <c>--form</c> and
    /// <c>--form-string</c> (<see cref="SelectedHttpMethod.MultipartFormPost"/>) selected first; once one
    /// is selected, selecting another is refused, as curl 8.21.0 does. <see cref="SelectedHttpMethod.None"/>
    /// when none of them was given. Transfer setup reads it to refuse a <see cref="PostData"/> body
    /// sent with <c>HEAD</c> or <c>GET</c>.
    /// </summary>
    public SelectedHttpMethod HttpMethodSelected { get; internal set; }

    /// <summary>
    /// <see langword="true"/> when <c>-s</c> / <c>--silent</c> has been read and <c>-S</c> /
    /// <c>--show-error</c> has not, so far: curl then hides error messages.
    /// </summary>
    internal bool ErrorsHidden => Silent && !ShowError;

    /// <summary>
    /// The path of the default config file (<c>.curlrc</c>) read before the command line when every
    /// line of it was applied; <see langword="null"/> when none was found, when <c>-q</c> /
    /// <c>--disable</c> came first, or when a line of it was refused. curl 8.21.0 names it with
    /// <c>-v</c> as <c>Note: Read config file from '&lt;path&gt;'</c>.
    /// </summary>
    public string? DefaultConfigFile { get => globals.DefaultConfigFile; internal set => globals.DefaultConfigFile = value; }

    /// <summary>
    /// How many <c>-K</c> / <c>--config</c> files are being read right now, one inside another; curl
    /// refuses to open one more once <see cref="CommandLineRefusal.MaximumConfigFileDepth"/> are open.
    /// </summary>
    internal int OpenConfigFileCount { get => globals.OpenConfigFileCount; set => globals.OpenConfigFileCount = value; }

    /// <summary>
    /// The warning lines met while reading the command line, in command-line order, without
    /// line terminators. <see cref="CommandLineParser"/> hands them to <see cref="CommandLineParseResult.WarningLines"/>.
    /// </summary>
    internal IReadOnlyList<string> WarningLines => globals.WarningLines;

    /// <summary>
    /// Sets <see cref="HttpVersion"/>, first adding <see cref="CommandLineWarning.OverridesPreviousHttpVersion"/>,
    /// unless <c>-s</c> came first, when an earlier option asked for a different version, as curl 8.21.0 does.
    /// </summary>
    /// <param name="version">The version the option asks for.</param>
    internal void SelectHttpVersion(RequestedHttpVersion version)
    {
        if (HttpVersion is not null && HttpVersion != version)
        {
            AddWarningLinesUnlessSilent(CommandLineWarning.OverridesPreviousHttpVersion);
        }

        HttpVersion = version;
    }

    /// <summary>
    /// <see langword="true"/> while the option being applied is the first one of its argument
    /// (<c>--verbose</c>, or the <c>v</c> of <c>-v</c> and of <c>-vs</c>, but not of <c>-sv</c>); set by
    /// <see cref="CommandLineParser"/> before each option it applies.
    /// </summary>
    internal bool FirstOptionOfArgument { get => globals.FirstOptionOfArgument; set => globals.FirstOptionOfArgument = value; }

    /// <summary>
    /// Applies <c>-v</c> / <c>--verbose</c>, or <c>--no-verbose</c> when <paramref name="on"/> is
    /// <see langword="false"/>, as curl 8.21.0 does: see <see cref="Verbosity"/> and <see cref="TraceTime"/>.
    /// The first <c>v</c> after a reset selects <see cref="TraceKind.Verbose"/>, first adding
    /// <see cref="CommandLineWarning.VerboseOverridesTrace"/>, unless <c>-s</c> came first, when a
    /// <c>--trace</c> or <c>--trace-ascii</c> was in effect.
    /// </summary>
    /// <param name="on"><see langword="false"/> for <c>--no-verbose</c>.</param>
    internal void SetVerbose(bool on)
    {
        if (!on || FirstOptionOfArgument)
        {
            Verbosity = 0;
            TraceTime = false;
            TraceIds = false;
            RemoveVerbosityTraceComponents();
        }

        if (!on)
        {
            Trace = TraceKind.None;
            TraceFile = null;
            globals.TraceConfigIds = false;
            globals.TraceConfigTime = false;
            ClearTraceComponents();
            return;
        }

        RaiseVerbosity();
    }

    /// <summary>
    /// Turns <c>--ssl</c> / <c>--ftp-ssl</c> on or off. Turning it on warns, unless silent, as curl 8.21.0
    /// warns that trying TLS and going on in plaintext is insecure.
    /// </summary>
    /// <param name="on"><see langword="true"/> for the option, <see langword="false"/> for its <c>--no-</c> spelling.</param>
    /// <param name="longName">The option's long name with its <c>--</c>, named in the warning.</param>
    internal void SetSslTry(bool on, string longName)
    {
        SslTry = on;
        if (on)
        {
            AddWarningLinesUnlessSilent(CommandLineWarning.InsecureSsl(longName));
        }
    }

    /// <summary>Takes <see cref="Verbosity"/> one step up, to at most 4, as one more <c>v</c> does.</summary>
    private void RaiseVerbosity()
    {
        const int MostVerbose = 4;
        if (Verbosity == 0)
        {
            SelectTrace(TraceKind.Verbose, null, CommandLineWarning.VerboseOverridesTrace);
        }
        else if (Verbosity == 1)
        {
            TraceTime = true;
            TraceIds = true;
        }

        if (Verbosity < MostVerbose)
        {
            Verbosity++;
            if (Verbosity > 1)
            {
                AddVerbosityTraceComponents(Verbosity);
            }
        }
    }

    /// <summary>
    /// Applies <c>--trace</c> (<see cref="TraceKind.HexDump"/>) or <c>--trace-ascii</c>
    /// (<see cref="TraceKind.AsciiDump"/>) to <paramref name="file"/>, first adding
    /// <see cref="CommandLineWarning.TraceOverridesEarlierTrace"/>, unless <c>-s</c> came first, when
    /// <c>-v</c> or the other kind of trace was in effect. <see cref="Verbosity"/> is left as it is.
    /// </summary>
    /// <param name="dump">The kind of dump the option asks for.</param>
    /// <param name="file">The non-empty file name, <c>-</c> for standard output.</param>
    /// <param name="longName">The option's long name with its <c>--</c>, for the warning.</param>
    internal void SelectTraceDump(TraceKind dump, string file, string longName) =>
        SelectTrace(dump, file, CommandLineWarning.TraceOverridesEarlierTrace(longName));

    /// <summary>Sets <see cref="Trace"/> and <see cref="TraceFile"/>, warning when another kind was in effect.</summary>
    private void SelectTrace(TraceKind trace, string? file, IReadOnlyList<string> overrideWarning)
    {
        if (Trace != TraceKind.None && Trace != trace)
        {
            AddWarningLinesUnlessSilent(overrideWarning);
        }

        Trace = trace;
        TraceFile = file;
    }

    /// <summary>
    /// Appends <paramref name="lines"/> to <see cref="WarningLines"/> unless <c>-s</c> /
    /// <c>--silent</c> has already been read: curl 8.21.0 drops a warning raised while <c>-s</c> is in
    /// effect, even with <c>-S</c> and even if <c>--no-silent</c> follows, and keeps one raised
    /// before a later <c>-s</c>.
    /// </summary>
    /// <param name="lines">One warning's lines, without line terminators.</param>
    internal void AddWarningLinesUnlessSilent(IReadOnlyList<string> lines)
    {
        if (!Silent)
        {
            globals.WarningLines.AddRange(lines);
        }
    }

    /// <summary>
    /// Appends <paramref name="lines"/> to <see cref="WarningLines"/> as they are: error lines curl
    /// prints while reading its default config file, already hidden, or not, by the caller.
    /// </summary>
    /// <param name="lines">The lines, without line terminators.</param>
    internal void AddErrorLines(IReadOnlyList<string> lines) => globals.WarningLines.AddRange(lines);

    /// <summary>
    /// Makes <paramref name="file"/> the <see cref="StandardErrorFile"/> and appends a
    /// <see cref="StandardErrorRedirect"/> for it to <see cref="StandardErrorRedirects"/>, at the
    /// warning lines met so far and with the <c>-s</c> in effect now.
    /// </summary>
    /// <param name="file">The file the <c>--stderr</c> names.</param>
    internal void RedirectStandardError(string file)
    {
        StandardErrorFile = file;
        globals.StandardErrorRedirects.Add(new(file, globals.WarningLines.Count, Silent));
    }

    /// <summary>
    /// Sets the <c>--variable</c> <paramref name="name"/> to <paramref name="content"/>, replacing any earlier
    /// content, and adds curl 8.21.0's <c>Note: Overwriting variable '&lt;name&gt;'</c> line when it had some
    /// and <c>-v</c>, <c>--trace</c> or <c>--trace-ascii</c> is in effect, <c>-s</c> or not (measured 2026-09-27).
    /// </summary>
    /// <param name="name">The variable's name: letters, digits and underscores, case-sensitive.</param>
    /// <param name="content">The variable's bytes.</param>
    internal void SetVariable(string name, byte[] content)
    {
        if (globals.Variables.ContainsKey(name) && Trace != TraceKind.None)
        {
            AddErrorLines(WrappedMessage.Lines("Note: ", $"Overwriting variable '{name}'"));
        }

        globals.Variables[name] = content;
    }

    /// <summary>Looks up the bytes of the <c>--variable</c> <paramref name="name"/>.</summary>
    /// <param name="name">The variable's name, case-sensitive.</param>
    /// <returns>The variable's bytes; <see langword="null"/> when no variable has that name.</returns>
    internal byte[]? FindVariable(string name) => globals.Variables.GetValueOrDefault(name);

    /// <summary>
    /// Appends <paramref name="url"/> to <see cref="Urls"/>, unchanged and unvalidated, then refuses it as
    /// <see cref="RefuseEtagOptionsWithSeveralUrls"/> does.
    /// </summary>
    /// <param name="url">A positional argument or a <c>--url</c> value.</param>
    /// <param name="spelledOption">The argument as typed: the URL itself, or <c>--url</c>.</param>
    /// <returns><see langword="null"/>, or the refusal of a second URL beside an etag option.</returns>
    internal CommandLineRefusal? AddUrl(string url, string spelledOption)
    {
        urls.Add(url);
        (urlOutputs.Find(output => output.Url is null) ?? AddUrlOutput()).Url = url;
        return RefuseEtagOptionsWithSeveralUrls(spelledOption);
    }

    /// <summary>
    /// Refuses the argument just read when this option group has an <c>--etag-save</c> or
    /// <c>--etag-compare</c> and more than one URL, as curl 8.21.0 does whichever comes last:
    /// <c>curl: The etag options only work on a single URL</c> (hidden when <see cref="ErrorsHidden"/>),
    /// then <c>curl: option &lt;spelled&gt;: is badly used here</c>. A glob in one URL and a URL in a
    /// later <c>--next</c> group are not refused (measured 2026-09-29, BL-619 Notes).
    /// </summary>
    /// <param name="spelledOption">The argument as typed.</param>
    /// <returns>The refusal, or <see langword="null"/> when the group has at most one URL or no etag option.</returns>
    internal CommandLineRefusal? RefuseEtagOptionsWithSeveralUrls(string spelledOption) =>
        (EtagSaveFile ?? EtagCompareFile) is not null && urls.Count > 1
            ? CommandLineRefusal.EtagOptionsWithSeveralUrls(spelledOption, ErrorsHidden)
            : null;

    /// <summary>Appends <paramref name="uploadFile"/> to <see cref="UploadFiles"/>; nothing is opened.</summary>
    /// <param name="uploadFile">A <c>-T</c> / <c>--upload-file</c> value, which may be empty.</param>
    internal void AddUploadFile(string uploadFile) => uploadFiles.Add(uploadFile);

    /// <summary>
    /// Pairs <paramref name="outputFile"/> with the next URL in <see cref="UrlOutputs"/>; nothing is
    /// opened or created.
    /// </summary>
    /// <param name="outputFile">A <c>-o</c> / <c>--output</c> value.</param>
    internal void AddOutputFile(string outputFile)
    {
        UrlOutput output = urlOutputs.Find(output => !output.HasOutputOption) ?? AddUrlOutput();
        output.FileName = outputFile;
        output.HasOutputOption = true;
    }

    /// <summary>
    /// Pairs <c>--out-null</c> with the next URL in <see cref="UrlOutputs"/>, which then discards its
    /// body instead of saving it under the remote name <see cref="RemoteNameAll"/> would give it.
    /// </summary>
    internal void PairDiscardedBody()
    {
        UrlOutput output = urlOutputs.Find(output => !output.HasOutputOption) ?? AddUrlOutput();
        output.DiscardsBody = true;
        output.UsesRemoteName = false;
        output.HasOutputOption = true;
    }

    /// <summary>
    /// Pairs <c>-O</c> / <c>--remote-name</c> (<paramref name="on"/> <see langword="true"/>) or
    /// <c>--no-remote-name</c> with the next URL in <see cref="UrlOutputs"/>. When no entry is left
    /// without an output option and <see cref="RemoteNameAll"/> is off, <c>--no-remote-name</c> is
    /// dropped, as curl 8.21.0 drops it: <c>--no-remote-name --no-remote-name u</c> gives no warning
    /// about more output options than URLs.
    /// </summary>
    /// <param name="on"><see langword="false"/> for the <c>--no-</c> spelling.</param>
    internal void PairRemoteName(bool on)
    {
        UrlOutput? output = urlOutputs.Find(output => !output.HasOutputOption);
        if (output is null)
        {
            if (!on && !RemoteNameAll)
            {
                return;
            }

            output = AddUrlOutput();
        }

        output.UsesRemoteName = on;
        output.HasOutputOption = true;
    }

    /// <summary>
    /// <see langword="true"/> when an output option has no URL to pair with, so curl 8.21.0 warns
    /// with <see cref="CommandLineWarning.MoreOutputOptionsThanUrls"/>.
    /// </summary>
    internal bool HasMoreOutputOptionsThanUrls =>
        urlOutputs.Exists(output => output.HasOutputOption && output.Url is null);

    private UrlOutput AddUrlOutput()
    {
        UrlOutput output = new(RemoteNameAll);
        urlOutputs.Add(output);
        return output;
    }

    /// <summary>
    /// Appends the UTF-8 bytes of <paramref name="data"/> to <see cref="PostData"/>, after a single
    /// <c>&amp;</c> when <see cref="PostData"/> already holds at least one byte, as curl 8.21.0 does.
    /// </summary>
    /// <param name="data">A <c>-d</c> / <c>--data</c> value, possibly empty.</param>
    internal void AppendPostData(string data) => AppendPostData(Encoding.UTF8.GetBytes(data));

    /// <summary>
    /// Appends <paramref name="data"/> to <see cref="PostData"/>, after a single <c>&amp;</c> when
    /// <see cref="PostData"/> already holds at least one byte, as curl 8.21.0 does.
    /// </summary>
    /// <param name="data">The bytes of one <c>-d</c> / <c>--data</c> piece, possibly empty.</param>
    internal void AppendPostData(byte[] data)
    {
        byte[] separator = PostData is { Length: > 0 } ? [(byte)'&'] : [];
        PostData = PostData is { } body ? [.. body.Span, .. separator, .. data] : data;
    }

    /// <summary>
    /// Appends <paramref name="data"/> to <see cref="PostData"/> with no separator and sets
    /// <see cref="SendsJson"/>, as curl 8.21.0 does for <c>--json</c>.
    /// </summary>
    /// <param name="data">The bytes of one <c>--json</c> value, possibly empty.</param>
    internal void AppendJsonData(byte[] data)
    {
        PostData = PostData is { } body ? [.. body.Span, .. data] : data;
        SendsJson = true;
    }

    /// <summary>
    /// Appends <paramref name="query"/> to <see cref="UrlQuery"/>, after a <c>&amp;</c> when
    /// <see cref="UrlQuery"/> is already set, even to empty text.
    /// </summary>
    /// <param name="query">One encoded <c>--url-query</c> value, possibly empty.</param>
    internal void AppendUrlQuery(string query) =>
        UrlQuery = UrlQuery is null ? query : $"{UrlQuery}&{query}";

    /// <summary>Sets <see cref="Credentials"/> from <paramref name="userAndPassword"/>, split at its first colon.</summary>
    /// <param name="userAndPassword">A <c>-u</c> / <c>--user</c> value, possibly empty.</param>
    internal void SetCredentials(string userAndPassword)
    {
        Credentials = SplitAtFirstColon(userAndPassword);
        userAwaitingPassword = UserWhosePasswordIsMissing(userAndPassword);
    }

    /// <summary>Sets <see cref="ProxyCredentials"/> from <paramref name="userAndPassword"/>, split at its first colon.</summary>
    /// <param name="userAndPassword">A <c>-U</c> / <c>--proxy-user</c> value, possibly empty.</param>
    internal void SetProxyCredentials(string userAndPassword)
    {
        ProxyCredentials = SplitAtFirstColon(userAndPassword);
        proxyUserAwaitingPassword = UserWhosePasswordIsMissing(userAndPassword);
    }

    private static NetworkCredential SplitAtFirstColon(string userAndPassword)
    {
        int colon = userAndPassword.IndexOf(':', StringComparison.Ordinal);
        return colon < 0
            ? new NetworkCredential(userAndPassword, string.Empty)
            : new NetworkCredential(userAndPassword[..colon], userAndPassword[(colon + 1)..]);
    }

    /// <summary>
    /// The value itself when it names a user with no password, which curl 8.21.0 prompts for: no
    /// colon, and not starting with <c>;</c>. <see langword="null"/> otherwise.
    /// </summary>
    private static string? UserWhosePasswordIsMissing(string userAndPassword) =>
        !userAndPassword.Contains(':', StringComparison.Ordinal) && !userAndPassword.StartsWith(';') ? userAndPassword : null;

    /// <summary>Whether libcurl's <c>setopt_ech</c> accepts <paramref name="mode"/> (see <see cref="EchModeIsMalformed"/>).</summary>
    private static bool LibcurlAcceptsEchMode(string mode) =>
        EchModesWithoutValue.Contains(mode) || HasValueAfter(mode, "ecl:") || HasValueAfter(mode, "pn:");

    /// <summary>Whether <paramref name="mode"/> starts with <paramref name="prefix"/> and goes on past it.</summary>
    private static bool HasValueAfter(string mode, string prefix) =>
        mode.Length > prefix.Length && mode.StartsWith(prefix, StringComparison.Ordinal);

    /// <summary>
    /// Asks <paramref name="passwordPrompt"/> for each password the command line left out, with
    /// curl 8.21.0's prompts, host first: when the last <c>-u</c> / <c>--user</c> value named a user
    /// with no password and no <c>--oauth2-bearer</c> was given,
    /// <c>Enter host password for user '&lt;user&gt;':</c>, recorded as the <see cref="Credentials"/>
    /// password; then, when the last <c>-U</c> / <c>--proxy-user</c> value named a user with no
    /// password, <c>Enter proxy password for user '&lt;user&gt;':</c>, recorded as the
    /// <see cref="ProxyCredentials"/> password. The user shown is the value up to its first
    /// <c>;</c> (curl's login options are not shown). Does nothing when no password is missing.
    /// When the command line has more than one option group, each prompt ends
    /// <c> on URL #&lt;n&gt;:</c> instead, <c>&lt;n&gt;</c> this group's number counting from 1, as
    /// curl 8.21.0's <c>checkpasswd</c> builds it for every group but a lone one.
    /// </summary>
    /// <remarks>
    /// Measured with the local curl 8.21.0 on 2026-09-26: <c>-u u --oauth2-bearer tok</c> never
    /// prompts; <c>-U p -u h</c> prompts for <c>'h'</c>'s host password first. The per-group prompt
    /// text is read from curl's <c>tool_paramhlp.c</c>, as curl reads the password from the console
    /// and not from standard input, which a recorded run cannot answer (BL-508 Notes).
    /// </remarks>
    /// <param name="passwordPrompt">Asks for the passwords.</param>
    internal void ReadMissingPasswords(IPasswordPrompt passwordPrompt)
    {
        string urlNumber = Groups.Count == 1 ? string.Empty : $" on URL #{globals.Groups.IndexOf(this) + 1}";
        if (userAwaitingPassword is { } user && BearerToken is null)
        {
            Credentials = new NetworkCredential(user, ReadPassword(passwordPrompt, "host", user, urlNumber));
            userAwaitingPassword = null;
        }

        if (proxyUserAwaitingPassword is { } proxyUser)
        {
            ProxyCredentials = new NetworkCredential(proxyUser, ReadPassword(passwordPrompt, "proxy", proxyUser, urlNumber));
            proxyUserAwaitingPassword = null;
        }
    }

    private static string ReadPassword(IPasswordPrompt passwordPrompt, string kind, string user, string urlNumber)
    {
        int loginOptions = user.IndexOf(';', StringComparison.Ordinal);
        string shownUser = loginOptions < 0 ? user : user[..loginOptions];
        return passwordPrompt.ReadPassword($"Enter {kind} password for user '{shownUser}'{urlNumber}:");
    }

    /// <summary>
    /// Adds <paramref name="scheme"/> to, or for its <c>--no-</c> spelling removes it from, the
    /// schemes <c>--basic</c>, <c>--digest</c>, <c>--ntlm</c>, <c>--negotiate</c>, <c>--anyauth</c> and
    /// <c>--oauth2-bearer</c> asked for.
    /// </summary>
    /// <param name="scheme">The scheme the option names.</param>
    /// <param name="on"><see langword="false"/> for the <c>--no-</c> spelling.</param>
    internal void WantAuthScheme(HttpAuthSchemes scheme, bool on) =>
        wantedAuthSchemes = on ? wantedAuthSchemes | scheme : wantedAuthSchemes & ~scheme;

    /// <summary>Replaces every scheme asked for so far with every scheme there is, for <c>--anyauth</c>.</summary>
    internal void WantEveryAuthScheme()
    {
        wantedAuthSchemes = HttpAuthSchemes.Any | HttpAuthSchemes.Bearer;
        everyAuthSchemeWanted = true;
    }

    /// <summary>
    /// The schemes the authentication options asked for, before <see cref="AuthSchemes"/> drops a Bearer
    /// with no token and falls back to Basic; <see cref="HttpAuthSchemes.None"/> when none was asked for.
    /// </summary>
    internal HttpAuthSchemes RequestedAuthSchemes => wantedAuthSchemes;

    /// <summary>
    /// <see langword="true"/> once <c>--anyauth</c> was given: curl 8.21.0's tool then starts from libcurl's
    /// <c>CURLAUTH_ANY</c>, which a later <c>--no-</c> scheme option takes bits out of.
    /// </summary>
    internal bool EveryAuthSchemeRequested => everyAuthSchemeWanted;

    /// <summary>
    /// <see langword="true"/> when any of <c>--proxy-basic</c>, <c>--proxy-digest</c>, <c>--proxy-ntlm</c>,
    /// <c>--proxy-negotiate</c> and <c>--proxy-anyauth</c> is on, so curl 8.21.0's tool sets the proxy's
    /// schemes rather than leaving libcurl's default.
    /// </summary>
    internal bool ProxyAuthSchemeRequested => proxyAnyAuthWanted || wantedProxyAuthSchemes != HttpAuthSchemes.None;

    /// <summary>
    /// Turns on, or for its <c>--no-</c> spelling off, the switch <c>--proxy-basic</c>,
    /// <c>--proxy-digest</c>, <c>--proxy-ntlm</c> or <c>--proxy-negotiate</c> names; see <see cref="ProxyAuthSchemes"/>.
    /// </summary>
    /// <param name="scheme">The scheme the option names.</param>
    /// <param name="on"><see langword="false"/> for the <c>--no-</c> spelling.</param>
    internal void WantProxyAuthScheme(HttpAuthSchemes scheme, bool on) =>
        wantedProxyAuthSchemes = on ? wantedProxyAuthSchemes | scheme : wantedProxyAuthSchemes & ~scheme;

    /// <summary>Turns <c>--proxy-anyauth</c> on, or for <c>--no-proxy-anyauth</c> off; see <see cref="ProxyAuthSchemes"/>.</summary>
    /// <param name="on"><see langword="false"/> for the <c>--no-</c> spelling.</param>
    internal void WantEveryProxyAuthScheme(bool on) => proxyAnyAuthWanted = on;

    /// <summary>
    /// Records an <c>--oauth2-bearer</c> token as <see cref="BearerToken"/> and adds
    /// <see cref="HttpAuthSchemes.Bearer"/> to the schemes asked for, as curl 8.21.0's tool does.
    /// </summary>
    /// <param name="token">The non-empty token.</param>
    internal void SetBearerToken(string token)
    {
        BearerToken = token;
        WantAuthScheme(HttpAuthSchemes.Bearer, on: true);
    }

    /// <summary>
    /// Records a <c>-x</c> / <c>--proxy</c> value, or a <c>--proxy1.0</c>, <c>--socks4</c>, <c>--socks4a</c>,
    /// <c>--socks5</c> or <c>--socks5-hostname</c> one, as <see cref="Proxy"/>, replacing any earlier one.
    /// </summary>
    /// <param name="address">The value as given, possibly empty.</param>
    /// <param name="kindWithoutScheme">The kind the option names, used when the value has no scheme.</param>
    internal void SetProxy(string address, ProxyKind kindWithoutScheme) =>
        Proxy = new CommandLineProxy(address, kindWithoutScheme);

    /// <summary>Appends <paramref name="telnetOption"/> to <see cref="TelnetOptions"/>, unchanged and unvalidated.</summary>
    /// <param name="telnetOption">A <c>-t</c> / <c>--telnet-option</c> value, possibly empty.</param>
    internal void AddTelnetOption(string telnetOption) => telnetOptions.Add(telnetOption);

    /// <summary>Appends <paramref name="recipient"/> to <see cref="MailRecipients"/>, unchanged and unvalidated.</summary>
    /// <param name="recipient">A <c>--mail-rcpt</c> value, possibly empty.</param>
    internal void AddMailRecipient(string recipient) => mailRecipients.Add(recipient);

    /// <summary>Appends <paramref name="quoteCommand"/> to <see cref="QuoteCommands"/>, unchanged and unvalidated.</summary>
    /// <param name="quoteCommand">A <c>-Q</c> / <c>--quote</c> value, possibly empty.</param>
    internal void AddQuoteCommand(string quoteCommand) => quoteCommands.Add(quoteCommand);

    /// <summary>Appends <paramref name="entry"/> to <see cref="ResolveEntries"/>, unchanged and unvalidated.</summary>
    /// <param name="entry">A <c>--resolve</c> value, possibly empty.</param>
    internal void AddResolveEntry(string entry) => resolveEntries.Add(entry);

    /// <summary>Appends <paramref name="entry"/> to <see cref="ConnectToEntries"/>, unchanged and unvalidated.</summary>
    /// <param name="entry">A <c>--connect-to</c> value, possibly empty.</param>
    internal void AddConnectToEntry(string entry) => connectToEntries.Add(entry);

    /// <summary>Sets <see cref="UnixSocketPath"/> and <see cref="UnixSocketIsAbstract"/>, replacing any earlier socket.</summary>
    /// <param name="path">A non-empty <c>--unix-socket</c> or <c>--abstract-unix-socket</c> value.</param>
    /// <param name="isAbstract">Whether it came from <c>--abstract-unix-socket</c>.</param>
    internal void SetUnixSocket(string path, bool isAbstract)
    {
        UnixSocketPath = path;
        UnixSocketIsAbstract = isAbstract;
    }

    /// <summary>Appends <paramref name="cookie"/> to <see cref="Cookies"/>, unchanged and unvalidated.</summary>
    /// <param name="cookie">A <c>-b</c> / <c>--cookie</c> value, possibly empty.</param>
    internal void AddCookie(string cookie) => cookies.Add(new CommandLineCookie(cookie));

    /// <summary>Appends <paramref name="header"/> to <see cref="Headers"/>, unchanged and unvalidated.</summary>
    /// <param name="header">A <c>-H</c> / <c>--header</c> value, or one line of its <c>@file</c>.</param>
    internal void AddHeader(string header) => headers.Add(header);

    /// <summary>Appends <paramref name="header"/> to <see cref="ProxyHeaders"/>, unchanged and unvalidated.</summary>
    /// <param name="header">A <c>--proxy-header</c> value, or one line of its <c>@file</c>.</param>
    internal void AddProxyHeader(string header) => proxyHeaders.Add(header);

    /// <summary>
    /// Appends <paramref name="part"/> to the innermost multipart part still open, or to
    /// <see cref="FormParts"/> when none is.
    /// </summary>
    /// <param name="part">The part to append.</param>
    internal void AddFormPart(FormPartSpecification part)
    {
        if (openMultiparts.TryPeek(out FormPartSpecification? multipart))
        {
            multipart.AddPart(part);
        }
        else
        {
            formParts.Add(part);
        }
    }

    /// <summary>
    /// Appends <paramref name="multipart"/> as <see cref="AddFormPart"/> does and opens it, so the
    /// parts that follow go inside it until <see cref="TryCloseMultipart"/>.
    /// </summary>
    /// <param name="multipart">A <see cref="FormPartKind.Multipart"/> part.</param>
    internal void OpenMultipart(FormPartSpecification multipart)
    {
        AddFormPart(multipart);
        openMultiparts.Push(multipart);
    }

    /// <summary>Closes the innermost multipart part still open.</summary>
    /// <returns><see langword="true"/> when one was closed; <see langword="false"/> when none is open.</returns>
    internal bool TryCloseMultipart() => openMultiparts.TryPop(out _);
}
