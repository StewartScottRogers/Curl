using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1486.pl</c> (curl 8.22.0), which case 1486 runs as
/// <c>%SRCDIR/test1486.pl %SRCDIR</c> to check that the write-out variables <c>docs/cmdline-opts/write-out.md</c>
/// documents are the ones <c>src/tool_writeout.c</c> lists, in alphabetical order, without running Perl (BL-2865).
/// </summary>
/// <remarks>
/// Given the folder the command names (<c>.</c> when it names none or an empty one), it reads
/// <c>FOLDER/../src/tool_writeout.c</c>, whose variables are the <c>  { "name"</c> lines from
/// <c>static const struct writeoutvar</c> to the next line starting <c>}</c>, numbered from 1, and
/// <c>FOLDER/../docs/cmdline-opts/write-out.md</c>, whose variables are its <c>## `name`</c> headings up to a bare
/// <c>##</c> line, less <c>header{name}</c>, <c>output{filename}</c> and <c>time{format}</c>. To standard output it
/// writes, in the script's order and words, fewer than 10 variables found on either side, each source variable
/// (in ordinal order) not documented or out of alphabetical order, and each documented variable not in the source,
/// then <c>OK</c> when it found none; the exit code is the number of problems, modulo 256 as Perl's <c>exit</c>
/// takes it. A missing file reads as empty, as the script's unchecked <c>open</c> does. Parsing is by hand, not
/// with <c>Regex</c>, as elsewhere in this library.
/// </remarks>
internal static class UpstreamTest1486Script
{
    private const int MinimumVariableCount = 10;

    private const int ExitCodeMask = 255;

    private const string SourceVariablePrefix = "  { \"";

    private const string DocumentedVariablePrefix = "## `";

    private static readonly HashSet<string> SpecialVariables = new(["header{name}", "output{filename}", "time{format}"], StringComparer.Ordinal);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1486.pl";

    /// <summary>Runs the check over the folder a case's command names, writing to the invocation's standard output.</summary>
    /// <param name="invocation">The command's words after the script: the release's <c>tests</c> folder, or none.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string standardOutput) = Check(invocation.Arguments is [{ Length: > 0 } root, ..] ? root : ".");
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(standardOutput));
        return exitCode;
    }

    /// <summary>Checks the write-out variables of <c>tool_writeout.c</c> against <c>write-out.md</c>.</summary>
    /// <param name="root">The folder beside <c>src</c> and <c>docs</c>, the release's <c>tests</c> folder.</param>
    /// <returns>The script's exit code and what it writes to standard output.</returns>
    public static (int ExitCode, string StandardOutput) Check(string root)
    {
        Dictionary<string, int> inSource = SourceVariables(Path.Combine(root, "..", "src", "tool_writeout.c"));
        HashSet<string> inDocs = DocumentedVariables(Path.Combine(root, "..", "docs", "cmdline-opts", "write-out.md"));
        List<string> problems = inDocs.Count < MinimumVariableCount || inSource.Count < MinimumVariableCount ? ["problems to extract variables"] : [];
        problems.AddRange(SourceProblems(inSource, inDocs));
        problems.AddRange(inDocs.Order(StringComparer.Ordinal).Where(variable => !inSource.ContainsKey(variable)).Select(variable => $"{variable} documented, but not used in source code"));
        return (problems.Count & ExitCodeMask, problems.Count == 0 ? "OK\n" : string.Concat(problems.Select(problem => problem + "\n")));
    }

    // Each source variable, in ordinal order, must be documented and numbered by its place in that order.
    private static IEnumerable<string> SourceProblems(Dictionary<string, int> inSource, HashSet<string> inDocs)
    {
        int expectedNumber = 1;
        foreach (string variable in inSource.Keys.Order(StringComparer.Ordinal))
        {
            if (!inDocs.Contains(variable))
            {
                yield return $"{variable} is not mentioned in write.out.md";
            }

            if (inSource[variable] != expectedNumber++)
            {
                yield return $"{variable} is not in alphabetical order";
            }
        }
    }

    // getsrcvars: a later line naming the same variable renumbers it, as the script's hash assignment does.
    private static Dictionary<string, int> SourceVariables(string path)
    {
        Dictionary<string, int> variables = new(StringComparer.Ordinal);
        int number = 1;
        foreach (string line in Lines(path)
            .SkipWhile(line => !line.StartsWith("static const struct writeoutvar", StringComparison.Ordinal))
            .TakeWhile(line => !line.StartsWith('}'))
            .Where(line => line.StartsWith(SourceVariablePrefix, StringComparison.Ordinal)))
        {
            variables[UpTo(line[SourceVariablePrefix.Length..], '"')] = number++;
        }

        return variables;
    }

    // getdocsvars: /^## `([^`]*)`/ headings up to a /^## *\z/ line.
    private static HashSet<string> DocumentedVariables(string path) =>
        [.. Lines(path)
            .TakeWhile(line => !(line.StartsWith("##", StringComparison.Ordinal) && line.AsSpan(2).TrimStart(' ').IsEmpty))
            .Where(line => line.StartsWith(DocumentedVariablePrefix, StringComparison.Ordinal) && line.IndexOf('`', DocumentedVariablePrefix.Length) >= 0)
            .Select(line => UpTo(line[DocumentedVariablePrefix.Length..], '`'))
            .Where(variable => !SpecialVariables.Contains(variable))];

    private static IEnumerable<string> Lines(string path) => File.Exists(path) ? File.ReadLines(path) : [];

    private static string UpTo(string text, char end) => text.IndexOf(end) is var index and >= 0 ? text[..index] : text;
}
