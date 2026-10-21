using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>tests/test1222.pl</c> (curl 8.22.0), which case 1222 runs as
/// <c>%SRCDIR/test1222.pl %SRCDIR/.. %PWD/..</c> to check that headers, man pages and
/// <c>docs/libcurl/symbols-in-versions</c> agree on deprecations, without running Perl (BL-2992).
/// </summary>
/// <remarks>
/// It reads <c>ROOT/docs/libcurl/symbols-in-versions</c>, whose absence is the script's <c>die</c> (its message to
/// standard error and exit code 255), then looks for the generated man pages, <c>BUILDROOT/docs/libcurl/*.3</c>. With
/// none, as in a release whose manual was not built (the vendored data holds only the Markdown sources), it writes the
/// script's <c>curl built without the libcurl manual. Skipping test 1222.</c> to standard output and exits 0, as the
/// script does. The full check over generated pages is not interpreted yet (BL-2996): with <c>.3</c> pages present it
/// says so on standard error and exits 1, so the case fails rather than passing unchecked.
/// </remarks>
internal static class UpstreamTest1222Script
{
    private const int Die = 255;

    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "test1222.pl";

    /// <summary>Runs the check over the folders a case's command names, writing to the invocation's output streams.</summary>
    /// <param name="invocation">The command's words after the script: the release root and the build root.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public static async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        (int exitCode, string standardOutput, string standardError) = Check(invocation.Arguments[0], invocation.Arguments[1]);
        await invocation.StandardOutput.WriteAsync(Encoding.UTF8.GetBytes(standardOutput));
        await invocation.StandardError.WriteAsync(Encoding.UTF8.GetBytes(standardError));
        return exitCode;
    }

    /// <summary>Checks for symbols-in-versions and the generated libcurl man pages.</summary>
    /// <param name="root">The release root, beside <c>docs</c> and <c>include</c>, as the script prints it.</param>
    /// <param name="buildRoot">The build root, whose <c>docs/libcurl</c> holds the generated <c>.3</c> pages.</param>
    /// <returns>The script's exit code and what it writes to standard output and standard error.</returns>
    public static (int ExitCode, string StandardOutput, string StandardError) Check(string root, string buildRoot)
    {
        string symbolsInVersions = $"{root}/docs/libcurl/symbols-in-versions";
        if (!File.Exists(symbolsInVersions))
        {
            return (Die, string.Empty, symbolsInVersions + "\n");
        }

        string manualFolder = $"{buildRoot}/docs/libcurl";
        return Directory.Exists(manualFolder) && Directory.EnumerateFiles(manualFolder, "*.3").Any()
            ? (1, string.Empty, "the deprecation check over the generated libcurl man pages is not interpreted (BL-2996)\n")
            : (0, "curl built without the libcurl manual. Skipping test 1222.\n", string.Empty);
    }
}
