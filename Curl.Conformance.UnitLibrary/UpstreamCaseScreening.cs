using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Decides whether the harness can run an expanded test case, and when it cannot, why: the
/// reason a case is reported <c>Inconclusive</c> and left out of the runnable count (ADR-0013,
/// decision 6).
/// </summary>
/// <remarks>
/// A case is skipped when its expansion stopped at a stray <c>%else</c> or <c>%endif</c> or left
/// something unresolved; when its postcheck runs test1013.pl or test1022.pl, which compare with
/// <c>../curl-config</c>; when it has a part the harness
/// does not act on (a <c>&lt;tool&gt;</c> libtest,
/// a <c>&lt;verify&gt;&lt;upload&gt;</c>, …); when a precheck or postcheck line is not a
/// <c>%PERL -e</c> one-liner <see cref="UpstreamPerlOneLiner"/> interprets; when it needs a server other than <c>http</c> (the
/// one emulated), <c>http-ipv6</c> (the same emulation on <c>%HOST6IP</c>:<c>%HTTP6PORT</c>), <c>http-unix</c> (the same emulation on the Unix socket <c>%HTTPUNIXPATH</c>),<c>http-proxy</c> (the same emulation on <c>%PROXYPORT</c>), <c>socks4</c> or <c>socks5</c> (<see cref="SocksServerConnector"/> on <c>%SOCKSPORT</c>), <c>mqtt</c> (<see cref="MqttServerConnector"/> on <c>%MQTTPORT</c>), <c>tftp</c> (<see cref="TftpServerConnector"/> on <c>%TFTPPORT</c>), <c>gopher</c> (<see cref="GopherServerConnector"/> on <c>%GOPHERPORT</c>),<c>ftp</c> (<see cref="FtpServerConnector"/> on <c>%FTPPORT</c>, control channel only, so a case whose <c>&lt;verify&gt;&lt;protocol&gt;</c> shows a data connection opening is skipped), <c>ftps</c> (<see cref="FtpsServerConnector"/> on <c>%FTPSPORT</c>, the FTP stand-in behind implicit TLS), <c>mqtts</c> and <c>gophers</c> (<see cref="MqttsServerConnector"/> and <see cref="GophersServerConnector"/>, the MQTT and Gopher stand-ins behind TLS), <c>rtsp</c> (<see cref="RtspServerConnector"/> on <c>%RTSPPORT</c>), <c>file</c> or <c>none</c>; when it needs a feature Curl lacks, or needs absent
/// one Curl has; when its <c>&lt;servercmd&gt;</c> holds a command the sws emulation (or, for an <c>ftp</c> or <c>ftps</c> case, the FTP stand-in) does not carry
/// out; when it names no server and its command goes to a host name on the internet; when its
/// command is not a plain curl command line; when a file part does not name an
/// absolute path; when a strip line is not a substitution the harness can run; or when an entry of
/// its comma-separated list of expected exit codes is not a number.
/// </remarks>
internal static class UpstreamCaseScreening
{
    // runtests.pl at curl-8_22_0 reads stdout only from <verify>, so a <client><stdout> (test210 to
    // test212, test407) is a comment it never checks; the harness accepts the case and ignores it too.
    // Nor do runtests.pl, runner.pm or servers.pm read a <client><connectport> (test1316): the
    // http-proxy sws tunnels to the port the CONNECT request names, as the proxy stand-in does (BL-2239).
    // test1651 nests its <verify><limits> inside <client>, where runtests.pl never looks for it, so that
    // <client><verify> is accepted and ignored too (BL-2913).
    private static readonly HashSet<string> ClientParts =
        ["name", "command", "server", "features", "file", "file1", "file2", "file3", "file4", "stdin", "stdout", "connectport", "killserver", "disable", "precheck", "setenv", "tool", "verify"];

    /// <summary>The reason a case is skipped whose <c>&lt;tool&gt;</c> names a libtest program with no port yet.</summary>
    public const string LibTestNotPorted = "libtest program not ported";

    /// <summary>The reason a case is skipped whose <c>&lt;tool&gt;</c> names a program other than a libtest one in a case without the <c>unittest</c> feature, whose unit program is screened instead (ADR-0711).</summary>
    public const string ToolNotRun = "the harness does not act on <client><tool>";

    // runtests.pl at curl-8_22_0 never reads a <verify><data> (test301's placeholder "[insert full protocol
    // verifiction dump here]", test302's empty one), so the harness accepts and ignores it too (BL-2222).
    private static readonly HashSet<string> VerifyParts =
    [
        "protocol", "proxy", "errorcode", "stdout", "stderr", "file", "file1", "file2", "file3", "file4", "notexists",
        "upload", "socks", "dns", "data", "strip", "strippart", "stripfile", "stripfile1", "stripfile2", "stripfile3", "stripfile4", "limits", "valgrind", "postcheck",
    ];

    /// <summary>
    /// The host test1592 sends its transfer to, whose name lookup must never answer: the harness's resolver
    /// stalls on it, so the case is not screened as reaching the internet (BL-2839, ADR-0722).
    /// </summary>
    public const string StalledLookupHost = "a-site-never-accessed.example.org";

    // Interpreted, not source-generated, so no generated code counts against the coverage gate.
    private static readonly Regex InternetUrlHost = new(@"\bhttps?://(?<host>[A-Za-z][A-Za-z0-9-]*(?:\.[A-Za-z0-9-]+)+)", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> Servers = ["http", "http-ipv6", "http-unix", "http-proxy", HttpsProxyServerConnector.EmulatedServer, "socks4", "socks5", "mqtt", "tftp", .. DnsServerConnector.EmulatedServers, "gopher", "gopher-ipv6", "ftp-ipv6", "file", "none", .. LineProtocolServerConnector.EmulatedServers, .. MailTlsServerConnector.EmulatedServers, .. FtpsServerConnector.EmulatedServers, .. MqttsServerConnector.EmulatedServers, .. GophersServerConnector.EmulatedServers, .. DictServerConnector.EmulatedServers, .. TelnetServerConnector.EmulatedServers, .. RtspServerConnector.EmulatedServers, "sftp", "scp"];

    // Upstream's nghttpx front of sws, emulated only when the runner is given an UpstreamHttp2Server.
    private const string Http2Server = "http/2";

    // Upstream's nghttpx-h3 front of sws, emulated only when the runner is given an UpstreamHttp3Server.
    private const string Http3Server = "http/3";

    // The servers that read <servercmd> as ftpserver.pl does: the FTP stand-in, plain or behind TLS.
    private static readonly string[] FtpServers = ["ftp", "ftp-ipv6", .. FtpsServerConnector.EmulatedServers];

    private static readonly Regex FtpDataConnectionCommand = new(@"^LPRT\b", RegexOptions.CultureInvariant | RegexOptions.Multiline);
    private static readonly Regex CurlConfigScript = new(@"\btest(?:1013|1022)\.pl\b", RegexOptions.CultureInvariant);

    private static readonly string[] FileParts = ["file", "file1", "file2", "file3", "file4"];

    private static readonly string[] SubstitutionParts = ["strippart", "stripfile", "stripfile1", "stripfile2", "stripfile3", "stripfile4"];

    /// <summary>Finds the first reason the harness cannot run the case.</summary>
    /// <param name="expansion">The case's expanded file.</param>
    /// <param name="testCase">The expanded file, parsed.</param>
    /// <param name="features">The features Curl reports.</param>
    /// <param name="emulatesHttp2">
    /// Whether the runner has the stand-in for upstream's <c>http/2</c> server (<see cref="UpstreamHttp2Server"/>), so a
    /// case naming that server can run (BL-2230).
    /// </param>
    /// <param name="emulatesHttp3">
    /// Whether the runner has the stand-in for upstream's <c>http/3</c> server (<see cref="UpstreamHttp3Server"/>), so a
    /// case naming that server can run (BL-2261).
    /// </param>
    /// <param name="isPortedLibTest">
    /// Whether a libtest program, by name, has a port the runner can run (BL-2556); <see langword="null"/>
    /// for none, so every case with a <c>&lt;tool&gt;</c> is skipped.
    /// </param>
    /// <param name="interpretsPerlScript">
    /// Whether a Perl script, by file name (<c>test1707.pl</c>), has a registered C# interpretation the runner
    /// runs in place of Perl (BL-2863); <see langword="null"/> for none, so every <c>&lt;command type="perl"&gt;</c>
    /// case is skipped.
    /// </param>
    /// <param name="isPortedUnitProgram">
    /// Whether a unit program, by name (<c>unit1302</c>, <c>tool1394</c>), has a port the runner can run (BL-2841,
    /// ADR-0711); <see langword="null"/> for none, so every <c>unittest</c> case is skipped as having no port.
    /// </param>
    /// <param name="testNumber">The case's number, which names the unit program of a <c>unittest</c> case with no <c>&lt;tool&gt;</c>.</param>
    /// <returns>A sentence saying why the case is skipped, or <see langword="null"/> when it can run.</returns>
    public static string? FindSkipReason(UpstreamTestFileExpansion expansion, UpstreamTestCase testCase, IReadOnlySet<string> features, bool emulatesHttp2 = false, bool emulatesHttp3 = false, Func<string, bool>? isPortedLibTest = null, Func<string, bool>? interpretsPerlScript = null, Func<string, bool>? isPortedUnitProgram = null, int testNumber = 0)
    {
        HashSet<string> standIns = StandIns(emulatesHttp2, emulatesHttp3);
        Func<string, bool> isPorted = OrNone(isPortedLibTest);
        Func<string, bool> interpretsScript = OrNone(interpretsPerlScript);
        string? unitProgram = UnitProgramName(testCase, testNumber);
        IReadOnlySet<string> caseFeatures = CaseFeatures(testCase, testNumber, features, isPortedUnitProgram);
        Func<string?>[] checks =
        [
            () => expansion.ConditionError,
            () => UnresolvedVariable(expansion),
            () => UnsupportedInstruction(expansion),
            () => CurlConfigComparison(testCase),
            () => UnsupportedPart(testCase, "client", ClientParts),
            () => unitProgram is null ? UnrunTool(testCase, isPorted) : UnportedUnitProgram(unitProgram, OrNone(isPortedUnitProgram)),
            () => UnsupportedPart(testCase, "verify", VerifyParts),
            () => UninterpretedCheck(testCase, "client", "precheck", line => UpstreamLibTestCheckLine.Parse(line) is { } check && isPorted(check.Program)),
            () => UninterpretedCheck(testCase, "verify", "postcheck", _ => false),
            () => UnsupportedServer(testCase, standIns),
            () => FtpDataConnection(testCase),
            () => UploadOffMailServer(testCase),
            () => UnsupportedFeature(testCase, caseFeatures),
            () => UnsupportedCommand(testCase, interpretsScript, unitProgram),
            () => UnsupportedServerCommand(testCase),
            () => InternetHost(testCase),
            () => UnsupportedFileName(testCase, "client"),
            () => UnsupportedFileName(testCase, "verify"),
            () => UnsupportedStripPattern(testCase),
            () => UnsupportedStripCode(testCase),
            () => UnsupportedErrorCode(testCase),
        ];
        return checks.Select(check => check()).FirstOrDefault(reason => reason is not null);
    }

    private static HashSet<string> StandIns(bool emulatesHttp2, bool emulatesHttp3) =>
        [.. emulatesHttp2 ? [Http2Server] : Array.Empty<string>(), .. emulatesHttp3 ? [Http3Server] : Array.Empty<string>()];

    // A lookup the caller did not give finds nothing.
    private static Func<string, bool> OrNone(Func<string, bool>? lookup) => lookup ?? (_ => false);

    /// <summary>
    /// Finds a <c>&lt;client&gt;</c> or <c>&lt;verify&gt;</c> file part that names a file outside the
    /// case's log directory, which the harness would write or read anywhere on the disk and never
    /// clean up.
    /// </summary>
    /// <param name="testCase">The expanded case, which <see cref="FindSkipReason"/> let run.</param>
    /// <param name="logDirectory">The case's log directory, <c>%LOGDIR</c>.</param>
    /// <returns>A sentence saying why the case is skipped, or <see langword="null"/> when every file is inside it.</returns>
    public static string? FindFileOutsideLogDirectory(UpstreamTestCase testCase, string logDirectory)
    {
        string inside = Path.TrimEndingDirectorySeparator(Path.GetFullPath(logDirectory)) + Path.DirectorySeparatorChar;
        return new[] { "client", "verify" }
            .SelectMany(section => FileParts.SelectMany(name => testCase.FindAll(section, name)))
            .FirstOrDefault(part => !Path.GetFullPath(part.GetAttribute("name")!).StartsWith(inside, StringComparison.OrdinalIgnoreCase)) is { } outside
            ? $"<{outside.Section}><{outside.Name}> names {outside.GetAttribute("name")}, outside the case's log directory"
            : RedirectOutside(testCase, inside);
    }

    // The command's > or >> redirect of standard output writes a file too (test321).
    private static string? RedirectOutside(UpstreamTestCase testCase, string inside) =>
        UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(testCase.Find("client", "command"))).StandardOutputFile is { } file
        && !Path.GetFullPath(file).StartsWith(inside, StringComparison.OrdinalIgnoreCase)
            ? $"the command redirects its output to {file}, outside the case's log directory"
            : null;

    private static string? UnresolvedVariable(UpstreamTestFileExpansion expansion) =>
        expansion.UnknownVariables.Count > 0
            ? $"the harness has no value for {string.Join(", ", expansion.UnknownVariables)}"
            : null;

    private static string? UnsupportedInstruction(UpstreamTestFileExpansion expansion) =>
        expansion.UnsupportedInstructions.Count > 0
            ? $"the harness does not carry out {string.Join(", ", expansion.UnsupportedInstructions)}"
            : null;

    private static string? UnsupportedPart(UpstreamTestCase testCase, string section, HashSet<string> supported) =>
        testCase.Sections.FirstOrDefault(part => part.Section == section && !supported.Contains(part.Name)) is { } part
            ? $"the harness does not act on <{section}><{part.Name}>"
            : null;

    /// <summary>
    /// The libtest program a case's <c>&lt;client&gt;&lt;tool&gt;</c> line names, such as <c>lib500</c>
    /// (<c>lib%TESTNUMBER</c> once expanded, or <c>libNNN</c>); <see langword="null"/> when it has no
    /// <c>&lt;tool&gt;</c> or names another kind of program.
    /// </summary>
    /// <param name="testCase">The parsed, expanded case.</param>
    /// <returns>The program's name, or <see langword="null"/>.</returns>
    public static string? LibTestProgramName(UpstreamTestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        string tool = UpstreamTestPartBodies.Text(testCase.Find("client", "tool")).Trim();
        return tool.Length > 3 && tool.StartsWith("lib", StringComparison.Ordinal) && tool[3..].All(char.IsAsciiDigit) ? tool : null;
    }

    /// <summary>
    /// The unit program a <c>unittest</c> case runs (ADR-0711): the <c>unit&lt;n&gt;</c> or <c>tool&lt;n&gt;</c> its
    /// <c>&lt;client&gt;&lt;tool&gt;</c> line names (<c>tool%TESTNUMBER</c> once expanded, <c>unit1305</c>), or
    /// <c>unit&lt;testNumber&gt;</c> when it has no <c>&lt;tool&gt;</c>, as <c>runtests.pl</c> runs one;
    /// <see langword="null"/> for a case without the <c>unittest</c> feature or whose tool is another kind of program.
    /// </summary>
    /// <param name="testCase">The parsed, expanded case.</param>
    /// <param name="testNumber">The case's number.</param>
    /// <returns>The program's name, or <see langword="null"/>.</returns>
    public static string? UnitProgramName(UpstreamTestCase testCase, int testNumber)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        if (!UpstreamTestPartBodies.Lines(testCase.Find("client", "features")).Contains(UnitTestFeature, StringComparer.Ordinal))
        {
            return null;
        }

        string tool = UpstreamTestPartBodies.Text(testCase.Find("client", "tool")).Trim();
        return tool.Length == 0 ? $"unit{testNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : UnitProgramPrefixes.Any(prefix => IsNumberedProgram(tool, prefix)) ? tool : null;
    }

    /// <summary>The feature upstream's unit test cases name, <c>unittest</c>.</summary>
    public const string UnitTestFeature = "unittest";

    // The features a ported unit program meets for its case (ADR-0711): unittest itself, and Debug and TrackMemory,
    // which describe the C build's debug hooks and allocator tracking that the port needs neither of.
    private static readonly string[] UnitProgramFeatures = [UnitTestFeature, "Debug", "TrackMemory"];

    private static readonly string[] UnitProgramPrefixes = ["unit", "tool"];

    private static bool IsNumberedProgram(string tool, string prefix) =>
        tool.Length > prefix.Length && tool.StartsWith(prefix, StringComparison.Ordinal) && !tool.AsSpan(prefix.Length).ContainsAnyExceptInRange('0', '9');

    /// <summary>
    /// The features a case is screened against: <paramref name="features"/>, and, for a <c>unittest</c> case whose
    /// unit program is ported, <c>unittest</c>, <c>Debug</c> and <c>TrackMemory</c> too, so the platform reports
    /// <c>unittest</c> only through the table of ported programs, never as a feature of its own (ADR-0711).
    /// </summary>
    /// <param name="testCase">The parsed, expanded case.</param>
    /// <param name="testNumber">The case's number.</param>
    /// <param name="features">The features Curl reports.</param>
    /// <param name="isPortedUnitProgram">Whether a unit program, by name, has a port; <see langword="null"/> for none.</param>
    /// <returns>The features to screen the case against.</returns>
    public static IReadOnlySet<string> CaseFeatures(UpstreamTestCase testCase, int testNumber, IReadOnlySet<string> features, Func<string, bool>? isPortedUnitProgram)
    {
        ArgumentNullException.ThrowIfNull(features);
        return UnitProgramName(testCase, testNumber) is { } program && OrNone(isPortedUnitProgram)(program)
            ? features.Union(UnitProgramFeatures).ToHashSet(StringComparer.Ordinal)
            : features;
    }

    /// <summary>The reason a <c>unittest</c> case is skipped whose unit program has no port yet.</summary>
    /// <param name="program">The program's name, such as <c>unit1302</c>.</param>
    /// <returns>A sentence naming the program.</returns>
    public static string UnitProgramNotPorted(string program) => $"Curl has no port of {program}";

    private static string? UnportedUnitProgram(string program, Func<string, bool> isPortedUnitProgram) =>
        isPortedUnitProgram(program) ? null : UnitProgramNotPorted(program);

    // runtests.pl runs a <tool> in place of curl; the harness runs a ported libtest program (BL-2556) and no other.
    private static string? UnrunTool(UpstreamTestCase testCase, Func<string, bool> isPortedLibTest) =>
        testCase.Find("client", "tool") is null
            ? null
            : LibTestProgramName(testCase) is not { } program
                ? ToolNotRun
                : isPortedLibTest(program) ? null : LibTestNotPorted;

    // A precheck or postcheck runs only when every line is a %PERL -e one-liner that
    // UpstreamPerlOneLiner interprets (BL-1933), a test610.pl or test613.pl line (BL-1894) or a %RESOLVE line UpstreamResolveCheck emulates (BL-1929),
    // or, in a precheck, a %LIBTESTS line running a ported libtest program (BL-2634).
    private static string? UninterpretedCheck(UpstreamTestCase testCase, string section, string name, Func<string, bool> runsLibTest) =>
        UpstreamTestPartBodies.Lines(testCase.Find(section, name))
            .FirstOrDefault(line => !UpstreamPerlCheckLine.Interprets(line) && !UpstreamResolveCheck.Interprets(line) && !runsLibTest(line)) is { } line
            ? $"the harness does not interpret the <{section}><{name}> line {line}"
            : null;

    /// <summary>
    /// The certificate file the case's HTTPS server presents, from its <c>&lt;server&gt;</c> line
    /// <c>https</c> (<see cref="HttpsServerConnector.DefaultCertificateFile"/>) or
    /// <c>https &lt;file&gt;</c>, as upstream's <c>servers.pm</c> reads it; <see langword="null"/>
    /// when the case names no HTTPS server.
    /// </summary>
    /// <param name="testCase">The parsed, expanded case.</param>
    /// <returns>The file name under <c>%CERTDIR/certs/</c>, or <see langword="null"/>.</returns>
    public static string? HttpsCertificateFile(UpstreamTestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        return UpstreamTestPartBodies.Lines(testCase.Find("client", "server")).Select(HttpsCertificateFile).FirstOrDefault(file => file is not null);
    }

    private static string? HttpsCertificateFile(string serverLine) =>
        serverLine.Split(' ', StringSplitOptions.RemoveEmptyEntries) switch
        {
            ["https"] => HttpsServerConnector.DefaultCertificateFile,
            ["https", var file] => file,
            _ => null,
        };

    // standIns: the servers emulated only when the runner is given their stand-in (http/2, http/3).
    private static string? UnsupportedServer(UpstreamTestCase testCase, HashSet<string> standIns) =>
        UpstreamTestPartBodies.Lines(testCase.Find("client", "server")).FirstOrDefault(server => !Servers.Contains(server) && HttpsCertificateFile(server) is null && !standIns.Contains(server)) is { } server
            ? $"the harness does not emulate the {server} server"
            : null;

    // The FTP stand-in serves passive and active mode, downloads, listings and uploads (BL-1906,
    // BL-1907); a case whose protocol log shows LPRT, which curl sends only after EPRT is refused
    // and ftpserver.pl does not deal with, or ftpserver.pl's simulated wildcard directory is not emulated.
    private static string? FtpDataConnection(UpstreamTestCase testCase) =>
        FtpDataConnectionCommand.Match(UpstreamTestPartBodies.Text(testCase.Find("verify", "protocol"))) is { Success: true } command
            ? $"the FTP stand-in does not carry out the case's {command.Value}"
            : null;

    // Only the SMTP, IMAP and FTP stand-ins, plain or behind TLS, the tftpd stand-in and the SSH stand-in's scp and SFTP record an upload (BL-1909, BL-1910, BL-1914, BL-1952, BL-1907, BL-1913, BL-1917, BL-1918); any other server's case
    // verifying <upload> keeps a reason.
    private static readonly string[] UploadingServers = ["smtp", "imap", "smtps", "imaps", "tftp", "ftp", "ftp-ipv6", "ftps", "scp", "sftp"];

    private static string? UploadOffMailServer(UpstreamTestCase testCase) =>
        testCase.Find("verify", "upload") is not null && !UpstreamTestPartBodies.Lines(testCase.Find("client", "server")).Intersect(UploadingServers).Any()
            ? "the harness records <verify><upload> only for the smtp, imap, smtps, imaps, tftp, ftp, ftps, scp and sftp servers"
            : null;

    /// <summary>
    /// Finds the feature that keeps the case off this platform when Curl on another platform meets
    /// every one of its <c>&lt;features&gt;</c>, such as <c>Schannel</c> off Windows, so the ratchet
    /// can hold a listed case only where it can run (ADR-0488). A feature in <paramref name="features"/>
    /// that no platform reports, such as the harness's <c>Debug</c> stand-in, counts as met on every
    /// platform, since the harness stands in for it there too (ADR-0497).
    /// </summary>
    /// <param name="testCase">The expanded file, parsed.</param>
    /// <param name="features">The features Curl reports on this platform, with any the harness stands in for.</param>
    /// <returns>A sentence naming the feature, or <see langword="null"/> when this platform meets the case's features or no platform does.</returns>
    public static string? FindFeatureOfAnotherPlatform(UpstreamTestCase testCase, IReadOnlySet<string> features)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        ArgumentNullException.ThrowIfNull(features);
        string[] standIns = [.. features.Where(feature => !UpstreamCurlPlatform.All.Any(platform => platform.Features.Contains(feature)))];
        return UnsupportedFeature(testCase, features) is { } reason
            && UpstreamCurlPlatform.All.Any(platform => UnsupportedFeature(testCase, platform.Features.Concat(standIns).ToHashSet(StringComparer.Ordinal)) is null)
            ? reason
            : null;
    }

    private static string? UnsupportedFeature(UpstreamTestCase testCase, IReadOnlySet<string> features)
    {
        foreach (string feature in UpstreamTestPartBodies.Lines(testCase.Find("client", "features")))
        {
            bool excluded = feature.StartsWith('!');
            string name = excluded ? feature[1..] : feature;
            if (features.Contains(name) == excluded)
            {
                return excluded ? $"the case needs Curl without the feature {name}" : $"Curl lacks the feature {name}";
            }
        }

        return null;
    }

    /// <summary>
    /// The file name of the Perl script a <c>&lt;command type="perl"&gt;</c> runs, its first word after the last
    /// <c>/</c> (<c>test1707.pl</c> for <c>%SRCDIR/test1707.pl %CURL ...</c>); <see langword="null"/> when the
    /// command is not of that type or has no word.
    /// </summary>
    /// <param name="testCase">The parsed, expanded case.</param>
    /// <returns>The script's file name, or <see langword="null"/>.</returns>
    public static string? PerlScriptName(UpstreamTestCase testCase)
    {
        ArgumentNullException.ThrowIfNull(testCase);
        UpstreamTestSection? command = testCase.Find("client", "command");
        return command?.GetAttribute("type") == "perl" && UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(command)).Arguments is [var script, ..]
            ? script[(script.LastIndexOf('/') + 1)..]
            : null;
    }

    // runtests.pl runs a <command type="perl"> with perl; the harness runs a registered C# interpretation of its script (BL-2863) and no other.
    private static string? UnrunCommandType(UpstreamTestCase testCase, string type, Func<string, bool> interpretsPerlScript) =>
        PerlScriptName(testCase) is { } script && interpretsPerlScript(script) ? null : $"the harness does not run a {type} command";

    private static string? UnsupportedCommand(UpstreamTestCase testCase, Func<string, bool> interpretsPerlScript, string? unitProgram)
    {
        // runtests.pl runs a <tool> with an empty command too: a libtest or unit program then gets no arguments (test501, test1302).
        if (testCase.Find("client", "command") is not { } command)
        {
            return LibTestProgramName(testCase) is null && unitProgram is null ? "the case has no <client><command>" : null;
        }

        if (command.GetAttribute("type") is { } type)
        {
            return UnrunCommandType(testCase, type, interpretsPerlScript);
        }

        return UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(command)).UnsupportedShellSyntax is { } syntax
            ? $"the command needs a shell for its {syntax}"
            : null;
    }

    // test1013.pl and test1022.pl compare curl --version with ../curl-config, a script of the
    // upstream build that Curl does not ship (BL-1932), so their cases cannot be measured.
    private static string? CurlConfigComparison(UpstreamTestCase testCase) =>
        CurlConfigScript.Match(UpstreamTestPartBodies.Text(testCase.Find("verify", "postcheck"))) is { Success: true } script
            ? $"{script.Value} compares with ../curl-config, which Curl does not ship"
            : null;

    // <servercmd> is read by the server the case runs: ftpserver.pl's commands for an ftp or ftps
    // case, which the FTP stand-in carries out (DELAY and SLOWDOWNDATA too, BL-1958), sws's otherwise.
    private static string? UnsupportedServerCommand(UpstreamTestCase testCase) =>
        UpstreamTestPartBodies.Lines(testCase.Find("client", "server")).Intersect(FtpServers).Any()
            ? null
            : UnsupportedSwsServerCommand(testCase);

    private static string? UnsupportedSwsServerCommand(UpstreamTestCase testCase) =>
        SwsServerCommands.Read((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span).UnsupportedCommands is [var first, ..]
            ? $"the sws emulation does not carry out the server command {first}"
            : null;

    // A case that names no server, expects success and sends its command to a host name with a
    // dot (test2043's https://revoked.badssl.com/) needs the internet, which the in-process harness
    // never reaches (BL-1858); one expecting a failure (test467's http://example.com) fails first.
    // StalledLookupHost is not on the internet: the harness's resolver stalls on it (test1592, BL-2839).
    private static string? InternetHost(UpstreamTestCase testCase) =>
        NamesNoServerAndExpectsSuccess(testCase)
            && InternetUrlHost.Match(UpstreamTestPartBodies.Text(testCase.Find("client", "command"))) is { Success: true } url
            && !string.Equals(url.Groups["host"].Value, StalledLookupHost, StringComparison.OrdinalIgnoreCase)
            ? $"the case reaches {url.Groups["host"].Value} on the internet, which the harness does not"
            : null;

    private static bool NamesNoServerAndExpectsSuccess(UpstreamTestCase testCase) =>
        testCase.Find("client", "server") is null
            && UpstreamTestPartBodies.Text(testCase.Find("verify", "errorcode")).Trim() is "" or "0";

    private static string? UnsupportedFileName(UpstreamTestCase testCase, string section) =>
        FileParts.SelectMany(name => testCase.FindAll(section, name))
            .FirstOrDefault(part => !Path.IsPathRooted(part.GetAttribute("name"))) is { } unnamed
            ? $"<{section}><{unnamed.Name}> does not name a file by an absolute path"
            : null;

    private static string? UnsupportedStripPattern(UpstreamTestCase testCase) =>
        UpstreamTestPartBodies.Lines(testCase.Find("verify", "strip"))
            .FirstOrDefault(line => UpstreamRegex.TryCreate(line, RegexOptions.None) is null) is { } pattern
            ? $"the strip pattern {pattern} is not a .NET regular expression"
            : null;

    private static string? UnsupportedStripCode(UpstreamTestCase testCase) =>
        SubstitutionParts
            .SelectMany(name => UpstreamTestPartBodies.Lines(testCase.Find("verify", name)))
            .FirstOrDefault(line => !UpstreamPerlSubstitution.DoesNothing(line) && IUpstreamPerlLineEdit.Parse(line) is null) is { } code
            ? $"the harness does not run the Perl {code}"
            : null;

    // A comma-separated list is accepted (test305's "77,60", BL-2222); any entry that is not a number is not.
    private static string? UnsupportedErrorCode(UpstreamTestCase testCase) =>
        UpstreamTestPartBodies.Text(testCase.Find("verify", "errorcode")).Trim() is { Length: > 0 } code && UpstreamCaseVerification.ExpectedExitCodes(code).Contains(int.MinValue)
            ? $"the expected exit code {code} is not a number"
            : null;
}
