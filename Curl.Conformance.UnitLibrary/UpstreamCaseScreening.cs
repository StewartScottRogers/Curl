using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Decides whether the harness can run an expanded test case, and when it cannot, why: the
/// reason a case is reported <c>Inconclusive</c> and left out of the runnable count (ADR-0013,
/// decision 6).
/// </summary>
/// <remarks>
/// A case is skipped when its expansion stopped at a stray <c>%else</c> or <c>%endif</c> or left
/// something unresolved; when it has a part the harness
/// does not act on (a <c>&lt;tool&gt;</c> libtest, a <c>&lt;precheck&gt;</c>, a <c>&lt;setenv&gt;</c>,
/// a <c>&lt;verify&gt;&lt;upload&gt;</c>, …); when it needs a server other than <c>http</c> (the
/// one emulated), <c>file</c> or <c>none</c>; when it needs a feature Curl lacks, or needs absent
/// one Curl has; when its <c>&lt;servercmd&gt;</c> holds a command the sws emulation does not carry
/// out; when its command is not a plain curl command line; when a file part does not name an
/// absolute path; when a strip line is not a substitution the harness can run; or when its expected
/// exit code is not a number.
/// </remarks>
internal static class UpstreamCaseScreening
{
    private static readonly HashSet<string> ClientParts =
        ["name", "command", "server", "features", "file", "file1", "file2", "file3", "file4", "stdin", "killserver", "disable"];

    private static readonly HashSet<string> VerifyParts =
    [
        "protocol", "errorcode", "stdout", "stderr", "file", "file1", "file2", "file3", "file4", "notexists",
        "strip", "strippart", "stripfile", "stripfile1", "stripfile2", "stripfile3", "stripfile4", "limits", "valgrind",
    ];

    private static readonly HashSet<string> Servers = ["http", "file", "none"];

    private static readonly string[] FileParts = ["file", "file1", "file2", "file3", "file4"];

    private static readonly string[] SubstitutionParts = ["strippart", "stripfile", "stripfile1", "stripfile2", "stripfile3", "stripfile4"];

    /// <summary>Finds the first reason the harness cannot run the case.</summary>
    /// <param name="expansion">The case's expanded file.</param>
    /// <param name="testCase">The expanded file, parsed.</param>
    /// <param name="features">The features Curl reports.</param>
    /// <returns>A sentence saying why the case is skipped, or <see langword="null"/> when it can run.</returns>
    public static string? FindSkipReason(UpstreamTestFileExpansion expansion, UpstreamTestCase testCase, IReadOnlySet<string> features)
    {
        Func<string?>[] checks =
        [
            () => expansion.ConditionError,
            () => UnresolvedVariable(expansion),
            () => UnsupportedInstruction(expansion),
            () => UnsupportedPart(testCase, "client", ClientParts),
            () => UnsupportedPart(testCase, "verify", VerifyParts),
            () => UnsupportedServer(testCase),
            () => UnsupportedFeature(testCase, features),
            () => UnsupportedCommand(testCase),
            () => UnsupportedServerCommand(testCase),
            () => UnsupportedFileName(testCase, "client"),
            () => UnsupportedFileName(testCase, "verify"),
            () => UnsupportedStripPattern(testCase),
            () => UnsupportedStripCode(testCase),
            () => UnsupportedErrorCode(testCase),
        ];
        return checks.Select(check => check()).FirstOrDefault(reason => reason is not null);
    }

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
            : null;
    }

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

    private static string? UnsupportedServer(UpstreamTestCase testCase) =>
        UpstreamTestPartBodies.Lines(testCase.Find("client", "server")).FirstOrDefault(server => !Servers.Contains(server)) is { } server
            ? $"the harness does not emulate the {server} server"
            : null;

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

    private static string? UnsupportedCommand(UpstreamTestCase testCase)
    {
        if (testCase.Find("client", "command") is not { } command)
        {
            return "the case has no <client><command>";
        }

        if (command.GetAttribute("type") is { } type)
        {
            return $"the harness does not run a {type} command";
        }

        return UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(command)).UnsupportedShellSyntax is { } syntax
            ? $"the command needs a shell for its {syntax}"
            : null;
    }

    private static string? UnsupportedServerCommand(UpstreamTestCase testCase) =>
        SwsServerCommands.Read((testCase.Find("reply", "servercmd")?.Content ?? ReadOnlyMemory<byte>.Empty).Span).UnsupportedCommands is [var first, ..]
            ? $"the sws emulation does not carry out the server command {first}"
            : null;

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
            .FirstOrDefault(line => !UpstreamPerlSubstitution.DoesNothing(line) && UpstreamPerlSubstitution.Parse(line) is null) is { } code
            ? $"the harness does not run the Perl {code}"
            : null;

    private static string? UnsupportedErrorCode(UpstreamTestCase testCase) =>
        UpstreamTestPartBodies.Text(testCase.Find("verify", "errorcode")).Trim() is { Length: > 0 } code && !int.TryParse(code, out _)
            ? $"the expected exit code {code} is not a number"
            : null;
}
