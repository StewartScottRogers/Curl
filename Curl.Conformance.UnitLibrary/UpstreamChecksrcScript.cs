using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>scripts/checksrc.pl</c> (curl 8.22.0), which case 1185 runs as
/// <c>%SRCDIR/../scripts/checksrc.pl -bmagicbad -balsobad %LOGDIR/code%TESTNUMBER.c</c>, without running Perl (BL-3416).
/// </summary>
/// <remarks>
/// The options (<c>-D</c>, <c>-W</c>, <c>-b</c>, <c>-a</c>, <c>-A</c>, <c>-i</c>, <c>-m</c>, <c>-v</c>, <c>-h</c>), the
/// usage text, <c>checksrc.skip</c>, <c>.checksrc</c>, the <c>!checksrc!</c> instructions and the end-of-file checks run as
/// the script runs them, in its order, with its messages and columns; the per-line checks plug into
/// <see cref="UpstreamChecksrcRun"/>'s seam (BL-3417, BL-3418). Lines lose a trailing CR LF or LF on every
/// platform (the script's Windows rule). A path that does not start with <c>./</c> or <c>../</c> gets the <c>-D</c>
/// directory in front, an absolute one too, since the harness's <c>%LOGDIR</c> is absolute where runtests.pl's is
/// relative (ADR-0806); an absolute path is opened as it is. <c>COPYRIGHTYEAR</c>, which asks git about the file, never
/// warns: the case's file is in no git repository. Files are read as Latin-1, a missing file throws, and so does a
/// <c>.checksrc</c> line the script dies on.
/// </remarks>
internal static class UpstreamChecksrcScript
{
    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "checksrc.pl";

    /// <summary>What the script wrote to standard output and standard error, and its exit code.</summary>
    /// <param name="Output">The script's standard output.</param>
    /// <param name="Error">The script's standard error.</param>
    /// <param name="ExitCode">5 when a problem was reported, otherwise 0.</param>
    public readonly record struct Result(string Output, string Error, int ExitCode);

    /// <summary>Runs the script over the command's words, writing to the invocation's streams.</summary>
    /// <param name="invocation">The command's words after the script: options and files.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        Result result = Check(invocation.Arguments);
        await invocation.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(result.Output));
        await invocation.StandardError.WriteAsync(Encoding.Latin1.GetBytes(result.Error));
        return result.ExitCode;
    }

    /// <summary>Checks the files the arguments name, as <c>checksrc.pl</c> given those arguments does.</summary>
    /// <param name="arguments">The options and files.</param>
    /// <returns>What the script writes and its exit code.</returns>
    public static Result Check(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return new UpstreamChecksrcRun(arguments).Run();
    }
}
