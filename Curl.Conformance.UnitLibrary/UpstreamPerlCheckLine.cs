namespace Curl.Conformance;

/// <summary>
/// Runs a <c>%PERL</c> precheck or postcheck line without Perl (BL-1894): a <c>-e</c> one-liner
/// <see cref="UpstreamPerlOneLiner"/> interprets, or a line whose program is upstream's
/// <c>tests/libtest/test610.pl</c> (<see cref="UpstreamTest610Script"/>) or <c>test613.pl</c>
/// (<see cref="UpstreamTest613Script"/>), whichever folder <c>%SRCDIR</c> names.
/// </summary>
internal static class UpstreamPerlCheckLine
{
    private static readonly string[] EmulatedScripts = [UpstreamTest610Script.ScriptName, UpstreamTest613Script.ScriptName];

    /// <summary>Whether a check line is <c>%PERL</c> running a one-liner or script the harness emulates.</summary>
    /// <param name="line">An expanded precheck or postcheck line.</param>
    /// <returns><see langword="true"/> when <see cref="RunLine"/> would run it.</returns>
    public static bool Interprets(string line) =>
        UpstreamPerlOneLiner.Interprets(line) || ScriptArguments(line) is not null;

    /// <summary>Runs a check line when it is <c>%PERL</c> running a one-liner or script the harness emulates.</summary>
    /// <param name="line">An expanded precheck or postcheck line.</param>
    /// <param name="operatingSystemName">Perl's <c>$^O</c> for the platform the case stands for.</param>
    /// <returns>What the line did, or <see langword="null"/> when the harness does not emulate it.</returns>
    public static UpstreamPerlOneLinerResult? RunLine(string line, string operatingSystemName) =>
        UpstreamPerlOneLiner.RunLine(line, operatingSystemName)
            ?? (ScriptArguments(line) is { } arguments ? UpstreamTest610Script.Run(arguments) ?? UpstreamTest613Script.Run(arguments) : null);

    private static string? ScriptArguments(string line)
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith(UpstreamPerlOneLiner.Program + " ", StringComparison.Ordinal))
        {
            return null;
        }

        string arguments = trimmed[(UpstreamPerlOneLiner.Program.Length + 1)..].TrimStart();
        string program = arguments.Split(' ', 2)[0];
        return EmulatedScripts.Contains(Path.GetFileName(program)) ? arguments : null;
    }
}
