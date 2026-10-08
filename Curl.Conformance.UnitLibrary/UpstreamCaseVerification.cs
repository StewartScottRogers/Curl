using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Compares what one run of a test case produced against the case's <c>&lt;verify&gt;</c> section
/// and <c>&lt;reply&gt;</c> data, the way <c>runtests.pl</c> at <c>curl-8_21_0</c> checks a finished
/// test, and names the first difference.
/// </summary>
/// <remarks>
/// <para>
/// Checked in this order: <c>&lt;protocol&gt;</c> against the bytes the server received, with
/// <c>&lt;strip&gt;</c> lines removed from both and <c>&lt;strippart&gt;</c> run on the received
/// lines; the <c>--output</c> file against <c>&lt;datacheck&gt;</c> (and <c>&lt;datacheck1&gt;</c> to
/// <c>&lt;datacheck9&gt;</c> after it), or <c>&lt;data&gt;</c> when there is none, unless
/// <c>&lt;data&gt;</c> says <c>nocheck</c> or both are empty and it does not say <c>sendzero</c>;
/// <c>&lt;stdout&gt;</c> and <c>&lt;stderr&gt;</c> with <c>&lt;stripfile&gt;</c> run on the output;
/// each <c>&lt;file&gt;</c> to <c>&lt;file4&gt;</c> against the named file with the matching
/// <c>&lt;stripfileN&gt;</c>; <c>&lt;notexists&gt;</c>; and last the exit code against
/// <c>&lt;errorcode&gt;</c>, 0 when absent.
/// </para>
/// <para>
/// Expected bodies have <c>nonewline</c> and <c>crlf</c> applied. Under <c>mode="text"</c> both
/// sides are normalized to CRLF, as upstream does on platforms that translate text, so the
/// comparison does not depend on the platform's line endings. A stripped line that ends up empty
/// is dropped, as upstream drops it.
/// </para>
/// </remarks>
internal static class UpstreamCaseVerification
{
    private static readonly string[] FileSuffixes = ["", "1", "2", "3", "4"];

    /// <summary>Finds the first way the run differs from what the case expects.</summary>
    /// <param name="testCase">The expanded case.</param>
    /// <param name="run">What the run produced.</param>
    /// <returns>A sentence naming the first difference, or <see langword="null"/> when the run matches.</returns>
    public static string? FindFirstDifference(UpstreamTestCase testCase, UpstreamCaseRun run) =>
        FindFirstDifference(testCase, run, UpstreamRegex.MatchTimeout);

    /// <summary>Finds the first way the run differs from what the case expects.</summary>
    /// <param name="testCase">The expanded case.</param>
    /// <param name="run">What the run produced.</param>
    /// <param name="stripMatchTimeout">How long one <c>&lt;strip&gt;</c> pattern match may run.</param>
    /// <returns>A sentence naming the first difference, or <see langword="null"/> when the run matches.</returns>
    public static string? FindFirstDifference(UpstreamTestCase testCase, UpstreamCaseRun run, TimeSpan stripMatchTimeout)
    {
        Func<string?>[] checks =
        [
            () => CompareProtocol(testCase, run.ReceivedBytes, stripMatchTimeout),
            () => CompareReplyData(testCase, run.OutputFileBytes),
            () => CompareOutput(testCase, "stdout", "stripfile", run.StandardOutput),
            () => CompareOutput(testCase, "stderr", "stripfile", run.StandardError),
            .. FileSuffixes.Select<string, Func<string?>>(suffix => () => CompareFile(testCase, suffix)),
            () => CheckNotExists(testCase),
            () => CompareExitCode(testCase, run.ExitCode),
        ];
        try
        {
            return checks.Select(check => check()).FirstOrDefault(difference => difference is not null);
        }
        catch (RegexMatchTimeoutException exception)
        {
            return $"the strip pattern {exception.Pattern} took longer than {exception.MatchTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds";
        }
    }

    private static string? CompareProtocol(UpstreamTestCase testCase, byte[] received, TimeSpan stripMatchTimeout)
    {
        if (testCase.Find("verify", "protocol") is not { } part)
        {
            return null;
        }

        Regex[] strips = UpstreamTestPartBodies.Lines(testCase.Find("verify", "strip"))
            .Select(line => UpstreamRegex.TryCreate(line, RegexOptions.None, stripMatchTimeout)).OfType<Regex>().ToArray();
        byte[] expected = Strip(Expected(part), strips, []);
        byte[] actual = Strip(received, strips, Substitutions(testCase, "strippart"));
        return UpstreamFirstDifference.Describe("<verify><protocol>", expected, actual);
    }

    private static string? CompareReplyData(UpstreamTestCase testCase, byte[] outputFile)
    {
        UpstreamTestSection? data = testCase.Find("reply", "data");
        byte[] expected = ExpectedReplyData(testCase, data);
        bool sendsZero = data?.IsAttributeSet("sendzero") == true;
        return data?.IsAttributeSet("nocheck") == true || expected.Length == 0 && !sendsZero
            ? null
            : UpstreamFirstDifference.Describe("the --output file against <reply><data>", Normalized(expected, data), Normalized(outputFile, data));
    }

    private static byte[] ExpectedReplyData(UpstreamTestCase testCase, UpstreamTestSection? data)
    {
        UpstreamTestSection[] checks = new[] { "datacheck", "datacheck1", "datacheck2", "datacheck3", "datacheck4", "datacheck5", "datacheck6", "datacheck7", "datacheck8", "datacheck9" }
            .Select(name => testCase.Find("reply", name)).OfType<UpstreamTestSection>().ToArray();
        UpstreamTestSection[] parts = checks.Length > 0 ? checks : new[] { data }.OfType<UpstreamTestSection>().ToArray();
        return parts.SelectMany(part => UpstreamTestPartBodies.WithoutFinalNewline(UpstreamTestPartBodies.Decoded(part), part)).ToArray();
    }

    private static string? CompareOutput(UpstreamTestCase testCase, string name, string stripPartName, byte[] output) =>
        testCase.Find("verify", name) is { } part
            ? UpstreamFirstDifference.Describe(
                $"<verify><{name}>",
                Normalized(UpstreamTestPartBodies.WithCrlf(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part), part), part),
                Normalized(Strip(output, [], Substitutions(testCase, stripPartName)), part))
            : null;

    private static string? CompareFile(UpstreamTestCase testCase, string suffix)
    {
        if (testCase.Find("verify", "file" + suffix) is not { } part)
        {
            return null;
        }

        string path = part.GetAttribute("name")!;
        byte[] actual = File.Exists(path) ? File.ReadAllBytes(path) : [];
        return UpstreamFirstDifference.Describe(
            $"<verify><file{suffix}> ({path})",
            Normalized(Expected(part), part),
            Normalized(Strip(actual, [], Substitutions(testCase, "stripfile" + suffix)), part));
    }

    private static string? CheckNotExists(UpstreamTestCase testCase) =>
        UpstreamTestPartBodies.Lines(testCase.Find("verify", "notexists")).FirstOrDefault(Path.Exists) is { } path
            ? $"<verify><notexists>: {path} exists"
            : null;

    private static string? CompareExitCode(UpstreamTestCase testCase, int exitCode)
    {
        int expected = int.TryParse(UpstreamTestPartBodies.Text(testCase.Find("verify", "errorcode")).Trim(), out int code) ? code : 0;
        return exitCode == expected ? null : $"<verify><errorcode>: expected exit code {expected}, got {exitCode}";
    }

    // nonewline before crlf, as runtests.pl chomps the last line before subnewlines: forcing CRLF
    // first would leave a carriage return the request never ends in.
    private static byte[] Expected(UpstreamTestSection part) =>
        UpstreamTestPartBodies.WithCrlf(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part), part);

    private static byte[] Normalized(byte[] body, UpstreamTestSection? part) =>
        part?.GetAttribute("mode") == "text" ? UpstreamTestSectionLineEndings.NormalizeText(body) : body;

    private static UpstreamPerlSubstitution[] Substitutions(UpstreamTestCase testCase, string name) =>
        UpstreamTestPartBodies.Lines(testCase.Find("verify", name))
            .Select(UpstreamPerlSubstitution.Parse).OfType<UpstreamPerlSubstitution>().ToArray();

    // Removes the lines a strip pattern matches, runs each substitution on the rest, and drops
    // the lines that end up empty.
    private static byte[] Strip(byte[] body, Regex[] strips, UpstreamPerlSubstitution[] substitutions)
    {
        StringBuilder result = new();
        foreach (string line in UpstreamTestLines.SplitAfterLineFeeds(Encoding.Latin1.GetString(body)))
        {
            if (!strips.Any(strip => strip.IsMatch(line)))
            {
                result.Append(substitutions.Aggregate(line, (text, substitution) => substitution.Apply(text)));
            }
        }

        return Encoding.Latin1.GetBytes(result.ToString());
    }
}
