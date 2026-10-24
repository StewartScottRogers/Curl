using System.Text;
using System.Text.RegularExpressions;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/runtests.pl</c> (curl 8.22.0) as case 1169 runs it,
/// <c>%SRCDIR/runtests.pl -L %PWD/%LOGDIR/test%TESTNUMBER-library.pl</c>, to check that the runner accepts <c>-L</c> and
/// exits with the code the library's <c>exit</c> statement gives, without running Perl (BL-3410, ADR-0803).
/// </summary>
/// <remarks>
/// <c>runtests.pl</c> <c>require</c>s each <c>-L</c> file as it reads its options, so a library holding a line
/// <c>exit N</c> ends the run there with exit code <c>N</c> (modulo 256, as a process exit code is). Only the command line
/// <c>-L FILE</c> is interpreted, and only a library holding such a line: any other command line, or a library with no
/// <c>exit</c> line (after which <c>runtests.pl</c> would go on to run test cases), throws
/// <see cref="NotSupportedException"/>. The library is read as text lines (Latin-1, as Perl reads bytes); a missing file
/// throws.
/// </remarks>
internal static class UpstreamTest1169Script
{
    private static readonly Regex ExitStatement = new(@"^\s*exit\s*\(?\s*(\d+)\s*\)?\s*;?\s*$", RegexOptions.CultureInvariant, UpstreamRegex.MatchTimeout);

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "runtests.pl";

    /// <summary>Runs <c>runtests.pl -L FILE</c>, ending with the library's exit code.</summary>
    /// <param name="invocation">The command's words after the script: <c>-L</c> and the library's path.</param>
    /// <param name="runCurl">The in-process curl, which the library never reaches.</param>
    /// <returns>The exit code of the library's <c>exit</c> line.</returns>
    public static Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return Task.FromResult(Run(invocation.Arguments));
    }

    /// <summary>Carries out a <c>runtests.pl</c> command line of the form <c>-L FILE</c>.</summary>
    /// <param name="arguments">The words after the script's name.</param>
    /// <returns>The exit code of the library's first <c>exit</c> line.</returns>
    public static int Run(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments is ["-L", string library]
            ? LibraryExitCode(library)
            : throw new NotSupportedException($"runtests.pl is interpreted only as -L FILE, not as: {string.Join(' ', arguments)}");
    }

    // The first `exit N` line of the library ends runtests.pl with N, as require runs it.
    private static int LibraryExitCode(string library)
    {
        Match? exit = File.ReadLines(library, Encoding.Latin1).Select(line => ExitStatement.Match(line)).FirstOrDefault(match => match.Success);
        return exit is null
            ? throw new NotSupportedException($"the library {library} holds no exit line, so runtests.pl would go on to run test cases")
            : (int)(long.Parse(exit.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) % 256);
    }
}
