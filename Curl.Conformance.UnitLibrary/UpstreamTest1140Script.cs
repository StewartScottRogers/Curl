using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1140.pl</c> (curl 8.22.0), which case 1140 runs as
/// <c>%SRCDIR/test1140.pl %PWD/../docs/ %PWD/../docs/libcurl/*.3 %PWD/../docs/libcurl/opts/*.3 %PWD/../docs/*.1 %PWD/../docs/cmdline-opts/*.1</c>
/// to scan the nroff man pages for unbalanced <c>\f</c> codes and references to man pages that do not exist, without
/// running Perl (BL-3408).
/// </summary>
/// <remarks>
/// The first word is the docs root; the others are the shell's globs, passed unexpanded, so each <c>*</c> in a file name
/// is expanded here, in ordinal order, over the files of its folder. Per line, each <c>\fX...\fY</c> pair is checked for
/// a double highlight, a missing <c>\fP</c> and, for a reference to a <c>curl...(3)</c> page, an <c>\fI</c> and the
/// page's existence; a <c>curl...(3)</c> left outside any font pair is a "non-referencing" error; and a <c>.BR</c> line's
/// <c>curl...</c> <c>"(3)"</c> references must exist. A page exists when it is a file under the docs root, its
/// <c>libcurl</c> folder or its <c>libcurl/opts</c> folder, or when <see cref="UpstreamCd2nroff"/> writes it from the
/// Markdown of <c>docs/libcurl</c>. A glob over <c>docs/libcurl</c> or <c>docs/libcurl/opts</c> that matches no built
/// page scans those converted pages instead, as a build would (ADR-0752); a glob that matches nothing elsewhere (the
/// <c>.1</c> pages that only <c>managen</c> writes) scans nothing. It writes each error and then <c>OK</c> when there
/// was none to standard output, and exits 1 on any error; <c>-g</c> as the root writes the usage. A named file that
/// cannot be opened throws, as the script's <c>die</c>.
/// </remarks>
internal static class UpstreamTest1140Script
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private static readonly Regex FontPair = new(@"\\f(.)([^ ]*)\\f(.)", Options, UpstreamRegex.MatchTimeout);

    private static readonly Regex DoubleHighlight = new(@"^\\f[ib]", Options | RegexOptions.IgnoreCase, UpstreamRegex.MatchTimeout);

    private static readonly Regex Reference = new(@"((libcurl|curl)([^ ]*))\(3\)", Options | RegexOptions.IgnoreCase, UpstreamRegex.MatchTimeout);

    private static readonly Regex NonReference = new(@"(curl([^ ]*)\(3\))", Options | RegexOptions.IgnoreCase, UpstreamRegex.MatchTimeout);

    private static readonly Regex BoldReference = new(@"((lib|)curl([^ ]*)) *""\(3\)(,|) *"" *", Options | RegexOptions.IgnoreCase, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1140.pl";

    /// <summary>Runs the scan over the pages a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the docs root, then the page globs.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code: 1 when it found an error, else 0.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string output) = Scan(invocation.Arguments);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(output));
        return exitCode;
    }

    /// <summary>Scans the pages the words name.</summary>
    /// <param name="arguments">The docs root, then the page globs.</param>
    /// <returns>The exit code and what the script writes to standard output.</returns>
    public static (int ExitCode, string Output) Scan(IReadOnlyList<string> arguments)
    {
        string docsRoot = DocsRoot(arguments);
        return docsRoot == "-g" ? (0, "Usage: test1140.pl <docs root dir> [manpages]\n") : Report(PageProblems(docsRoot, [.. arguments.Skip(1)]));
    }

    // $ARGV[0] || '.': the first word, unless it is missing, empty or "0".
    private static string DocsRoot(IReadOnlyList<string> arguments) =>
        arguments.Count > 0 && arguments[0] is not ("" or "0") ? arguments[0] : ".";

    private static (int ExitCode, string Output) Report(List<string> errors) =>
        (errors.Count == 0 ? 0 : 1, string.Concat(errors.Select(error => error + "\n")) + (errors.Count == 0 ? "OK\n" : string.Empty));

    private static List<string> PageProblems(string docsRoot, List<string> patterns)
    {
        Lazy<Dictionary<string, IReadOnlyList<string>>> converted = new(() => UpstreamCd2nroff.ConvertFolder($"{docsRoot}/libcurl"));
        Func<string, bool> isPresent = man => IsPresent(docsRoot, man, converted);
        return [.. patterns.SelectMany(pattern => Expand(docsRoot, pattern, converted)).SelectMany(page => PageErrors(page.Name, page.Lines, isPresent))];
    }

    // The pages a shell would pass for the word: a file as it is, a glob's matches, or for the manual's own folders the converted pages.
    private static IEnumerable<(string Name, IReadOnlyList<string> Lines)> Expand(string docsRoot, string pattern, Lazy<Dictionary<string, IReadOnlyList<string>>> converted)
    {
        int slash = pattern.LastIndexOf('/');
        string folder = slash < 0 ? "." : pattern[..slash];
        string name = pattern[(slash + 1)..];
        if (!name.Contains('*', StringComparison.Ordinal))
        {
            return File.Exists(pattern) ? [(pattern, File.ReadAllLines(pattern))] : throw new FileNotFoundException($"test1140.pl could not open {pattern}");
        }

        string suffix = name[(name.IndexOf('*', StringComparison.Ordinal) + 1)..];
        List<string> built = BuiltFiles(folder, suffix);
        return built.Count > 0
            ? built.Select(file => ($"{folder}/{file}", (IReadOnlyList<string>)File.ReadAllLines($"{folder}/{file}")))
            : ConvertedPages(docsRoot, folder, suffix, converted.Value);
    }

    private static List<string> BuiltFiles(string folder, string suffix) =>
        Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder).Select(Path.GetFileName).OfType<string>().Where(file => file.EndsWith(suffix, StringComparison.Ordinal)).Order(StringComparer.Ordinal)]
            : [];

    // The converted pages of the manual folder the glob names; none for any other folder.
    private static IEnumerable<(string Name, IReadOnlyList<string> Lines)> ConvertedPages(string docsRoot, string folder, string suffix, Dictionary<string, IReadOnlyList<string>> converted)
    {
        string full = Path.GetFullPath(folder);
        string prefix = full == Path.GetFullPath($"{docsRoot}/libcurl") ? string.Empty : full == Path.GetFullPath($"{docsRoot}/libcurl/opts") ? "opts/" : "\0";
        return converted
            .Where(page => page.Key.StartsWith(prefix, StringComparison.Ordinal) && page.Key.EndsWith(suffix, StringComparison.Ordinal) && page.Key.IndexOf('/', prefix.Length) < 0)
            .OrderBy(page => page.Key, StringComparer.Ordinal)
            .Select(page => ($"{folder}/{page.Key[prefix.Length..]}", page.Value));
    }

    private static bool IsPresent(string docsRoot, string man, Lazy<Dictionary<string, IReadOnlyList<string>>> converted) =>
        File.Exists($"{docsRoot}/{man}") || File.Exists($"{docsRoot}/libcurl/{man}") || File.Exists($"{docsRoot}/libcurl/opts/{man}")
        || converted.Value.ContainsKey(man) || converted.Value.ContainsKey($"opts/{man}");

    private static IEnumerable<string> PageErrors(string page, IReadOnlyList<string> lines, Func<string, bool> isPresent) =>
        lines.SelectMany((line, index) => LineErrors(page, index + 1, line, isPresent));

    private static List<string> LineErrors(string page, int number, string line, Func<string, bool> isPresent)
    {
        string where = $"error: {page}:{number}:";
        (List<string> errors, string rest) = FontErrors(where, line, isPresent);
        errors.AddRange(NonReference.Matches(rest).Take(1).Select(stray => $"{where} non-referencing {stray.Groups[1].Value}"));
        errors.AddRange(line.StartsWith(".BR ", StringComparison.Ordinal) ? BoldReferenceErrors(where, line[4..], isPresent) : []);
        return errors;
    }

    // Each \fX string \fY pair in turn, removed once it has been checked; the errors and the line that is left.
    private static (List<string> Errors, string Remainder) FontErrors(string where, string line, Func<string, bool> isPresent)
    {
        List<string> errors = [];
        string rest = line;
        for (Match pair = FontPair.Match(rest); pair.Success; pair = FontPair.Match(rest))
        {
            rest = rest.Remove(pair.Index, pair.Length);
            errors.AddRange(PairErrors(where, pair.Groups[1].Value, pair.Groups[2].Value, pair.Groups[3].Value, isPresent));
        }

        return (errors, rest);
    }

    // One \fX string \fY pair: not a double highlight, closed by \fP and, for a curl(3) reference, italic and existing.
    private static List<string> PairErrors(string where, string before, string text, string after, Func<string, bool> isPresent)
    {
        List<string> errors = [];
        if (DoubleHighlight.IsMatch(text))
        {
            errors.Add($"{where} double-highlight");
        }

        if (after != "P")
        {
            errors.Add($"{where} missing \\fP after {text}");
        }

        errors.AddRange(Reference.Matches(text).Take(1).SelectMany(reference => ReferenceErrors(where, before, text, reference.Groups[1].Value, isPresent)));
        return errors;
    }

    private static List<string> ReferenceErrors(string where, string before, string text, string name, Func<string, bool> isPresent)
    {
        string man = name.Replace("\\", string.Empty, StringComparison.Ordinal) + ".3";
        List<string> errors = [];
        if (!isPresent(man))
        {
            errors.Add($"{where} referring to non-existing man page {man}");
        }

        if (before != "I")
        {
            errors.Add($"{where} use \\fI before {text}");
        }

        return errors;
    }

    // The .BR line's curl...  "(3)"  references, each removed once it has been checked.
    private static List<string> BoldReferenceErrors(string where, string text, Func<string, bool> isPresent)
    {
        List<string> errors = [];
        for (Match reference = BoldReference.Match(text); reference.Success; reference = BoldReference.Match(text))
        {
            text = text.Remove(reference.Index, reference.Length);
            string man = reference.Groups[1].Value.Replace("\\", string.Empty, StringComparison.Ordinal) + ".3";
            errors.AddRange(isPresent(man) ? [] : [$"{where} referring to non-existing man page {man}"]);
        }

        return errors;
    }
}
