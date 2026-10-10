using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets the <c>%PERL -e '...'</c> one-liners upstream's cases put in a <c>&lt;precheck&gt;</c>
/// or <c>&lt;postcheck&gt;</c>, by recognising each form the vendored cases use rather than by
/// running Perl, which the harness neither has nor needs.
/// </summary>
/// <remarks>
/// The forms, each matched whole against the expanded line after the Perl program's name:
/// <list type="bullet">
/// <item><c>exit((stat("FILE"))[9] != EPOCH)</c>: exits 1 unless FILE's modification time is EPOCH
/// seconds (a missing file counts as 0, as Perl's undefined value does).</item>
/// <item><c>print 'TEXT' if('A' ne 'B');</c>: prints TEXT when A and B differ.</item>
/// <item><c>print 'TEXT' if($^O eq 'X' || ...);</c>: prints TEXT when the given Perl OS name is one of them.</item>
/// <item><c>if("HOST" !~ /PATTERN/) {print "TEXT"; exit(1)}</c>: prints TEXT and exits 1 when HOST does not match.</item>
/// <item><c>open(IN,$ARGV[0]); my $lines=grep(/PATTERN/, &lt;IN&gt;); exit ($lines != N); ...' FILE</c>:
/// exits 1 unless exactly N of FILE's lines match (a missing file has none).</item>
/// <item><c>for(1 .. N) { printf("TEXT", $_);}' &gt; FILE</c>: writes TEXT N times to FILE.</item>
/// <item><c>for my $i ((1..N)) { ... open(FH, "&gt;", ...); print FH "TEXT" ; close(FH) }</c>: writes TEXT
/// to PREFIX.1 to PREFIX.N, exiting 2 (Perl's <c>die $!</c> on ENOENT) when the folder is missing.</item>
/// <item><c>for my $i ((1..N)) { ... open(FH, "&lt;", ...); (&lt;FH&gt; eq "TEXT" and &lt;FH&gt; eq "") or die ... }</c>:
/// exits 2 when a file is missing and 255 (Perl's <c>die</c>) when one holds anything but TEXT.</item>
/// </list>
/// Any other line, such as test1083's <c>if ... else {exec '%RESOLVE ...'}</c>, is not interpreted.
/// </remarks>
internal static class UpstreamPerlOneLiner
{
    private const int DieOnMissingFile = 2;

    private const int Die = 255;

    // A file name in double-quoted Perl: the harness writes its paths literally, backslashes and all.
    private const string PathText = @"[^""$@%]*";

    // Double-quoted Perl text without interpolation, escapes or printf conversions.
    private const string PlainText = @"[^""$@%\\]*";

    // Interpreted, not source-generated, so no generated code counts against the coverage gate.
    private static readonly (Regex Pattern, Func<Match, string, UpstreamPerlOneLinerResult> Run)[] Forms =
    [
        (Form(@"-e 'exit\(\(stat\(""(?<file>[^""]+)""\)\)\[9\] != (?<epoch>-?\d+)\)'"), (match, _) => ModificationTimeIs(match)),
        (Form(@"-e ""print '(?<text>[^']*)' if\('(?<left>[^']*)' ne '(?<right>[^']*)'\);"""), (match, _) => PrintIfDifferent(match)),
        (Form(@"-e ""print '(?<text>[^']*)' if\((?<names>\$\^O eq '[^']+'(?: \|\| \$\^O eq '[^']+')*)\);"""), PrintIfOperatingSystem),
        (Form(@"-e 'if\(""(?<host>[^""]*)"" !~ /(?<pattern>[^/]*)/\) \{print ""(?<text>[^""]*)""; exit\(1\)\}'"), (match, _) => PrintIfNoMatch(match)),
        (Form(@"-e 'open\(IN,\$ARGV\[0\]\); my \$lines=grep\(/(?<pattern>[^/]*)/, <IN>\); exit \(\$lines != (?<count>\d+)\);[^']*' (?<file>\S+)"), (match, _) => MatchingLineCountIs(match)),
        (Form(@"-e 'for\(1 \.\. (?<count>\d+)\) \{ printf\(""(?<text>" + PlainText + @"(?:\\n" + PlainText + @")*)"", \$_\);\}' > (?<file>[^;\s]+);?"), (match, _) => WriteRepeated(match)),
        (Form(@"-e 'for my \$i \(\(1\.\.(?<count>\d+)\)\) \{ my \$filename = ""(?<prefix>" + PathText + @")\.\$i""; open\(FH, "">"", \$filename\) or die \$!; print FH ""(?<text>" + PlainText + @")"" ; close\(FH\) \}'"), (match, _) => WriteNumberedFiles(match)),
        (Form(@"-e 'for my \$i \(\(1\.\.(?<count>\d+)\)\) \{ my \$filename = ""(?<prefix>" + PathText + @")\.\$i""; open\(FH, ""<"", \$filename\) or die \$!; \(<FH> eq ""(?<text>" + PlainText + @")"" and <FH> eq """"\) or die ""incorrect \$filename"" ; close\(FH\) \}'"), (match, _) => NumberedFilesHold(match)),
    ];

    private static readonly UpstreamPerlOneLinerResult Passed = new(0, "");

    /// <summary>Runs a one-liner when it is one of the forms the harness interprets.</summary>
    /// <param name="arguments">The expanded line after the Perl program's name, starting with <c>-e</c>.</param>
    /// <param name="operatingSystemName">Perl's <c>$^O</c> for the platform the case stands for, such as <c>linux</c> or <c>MSWin32</c>.</param>
    /// <returns>What the one-liner did, or <see langword="null"/> when it is no form the harness interprets.</returns>
    public static UpstreamPerlOneLinerResult? Run(string arguments, string operatingSystemName)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string line = arguments.Trim();
        return Forms.Select(form => (form.Run, Match: form.Pattern.Match(line)))
            .FirstOrDefault(candidate => candidate.Match.Success) is { Run: { } run } found
            ? run(found.Match, operatingSystemName)
            : null;
    }

    private static Regex Form(string pattern) => new("^" + pattern + "$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    private static UpstreamPerlOneLinerResult ModificationTimeIs(Match match)
    {
        string file = match.Groups["file"].Value;
        long seconds = File.Exists(file) ? new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeSeconds() : 0;
        return new(seconds == long.Parse(match.Groups["epoch"].Value) ? 0 : 1, "");
    }

    private static UpstreamPerlOneLinerResult PrintIfDifferent(Match match) =>
        match.Groups["left"].Value == match.Groups["right"].Value ? Passed : new(0, match.Groups["text"].Value);

    private static UpstreamPerlOneLinerResult PrintIfOperatingSystem(Match match, string operatingSystemName) =>
        match.Groups["names"].Value.Contains($"'{operatingSystemName}'", StringComparison.Ordinal) ? new(0, match.Groups["text"].Value) : Passed;

    private static UpstreamPerlOneLinerResult PrintIfNoMatch(Match match) =>
        Regex.IsMatch(match.Groups["host"].Value, match.Groups["pattern"].Value, RegexOptions.None, UpstreamRegex.MatchTimeout)
            ? Passed
            : new(1, match.Groups["text"].Value);

    private static UpstreamPerlOneLinerResult MatchingLineCountIs(Match match)
    {
        string file = match.Groups["file"].Value;
        var pattern = new Regex(match.Groups["pattern"].Value, RegexOptions.None, UpstreamRegex.MatchTimeout);
        int lines = File.Exists(file) ? File.ReadLines(file).Count(pattern.IsMatch) : 0;
        return new(lines == int.Parse(match.Groups["count"].Value) ? 0 : 1, "");
    }

    private static UpstreamPerlOneLinerResult WriteRepeated(Match match)
    {
        string text = match.Groups["text"].Value.Replace(@"\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(match.Groups["file"].Value, string.Concat(Enumerable.Repeat(text, int.Parse(match.Groups["count"].Value))));
        return Passed;
    }

    private static UpstreamPerlOneLinerResult WriteNumberedFiles(Match match)
    {
        string prefix = match.Groups["prefix"].Value;
        if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(prefix))))
        {
            return new(DieOnMissingFile, "");
        }

        foreach (int number in Enumerable.Range(1, int.Parse(match.Groups["count"].Value)))
        {
            File.WriteAllText($"{prefix}.{number}", match.Groups["text"].Value);
        }

        return Passed;
    }

    private static UpstreamPerlOneLinerResult NumberedFilesHold(Match match)
    {
        string prefix = match.Groups["prefix"].Value;
        foreach (int number in Enumerable.Range(1, int.Parse(match.Groups["count"].Value)))
        {
            string file = $"{prefix}.{number}";
            if (!File.Exists(file))
            {
                return new(DieOnMissingFile, "");
            }

            if (File.ReadAllText(file) != match.Groups["text"].Value)
            {
                return new(Die, "");
            }
        }

        return Passed;
    }
}
