using System.Globalization;
using System.Text;
using Curl.Cli.ManualPages;

namespace Curl.Conformance;

/// <summary>
/// Interprets upstream's <c>scripts/managen</c> (curl 8.22.0), which cases 1705 and 1706 run as
/// <c>%SRCDIR/../scripts/managen -d %LOGDIR -I %SRCDIR/../include mainpage|ascii option1.md option2.md</c>, without
/// running Perl (BL-2881): Curl's own <see cref="ManualPageGenerator"/> writes the page from the case's files.
/// </summary>
/// <remarks>
/// The page's date is <c>SOURCE_DATE_EPOCH</c> from the invocation's environment as a UTC date, as the script reads
/// it, else today's local date from the time provider; <c>CURL_MAKETGZ_VERSION</c> overrides the version, as in the
/// script. Files are read and output written as Latin-1, so bytes pass through unchanged.
/// </remarks>
/// <param name="timeProvider">Gives today's date when the environment sets no <c>SOURCE_DATE_EPOCH</c>.</param>
public sealed class UpstreamManagenScript(TimeProvider timeProvider)
{
    /// <summary>The file name of the upstream script this class interprets.</summary>
    public const string ScriptName = "managen";

    /// <summary>Runs the script with the command's words after it.</summary>
    /// <param name="invocation">The script's arguments, environment and the case's streams.</param>
    /// <param name="runCurl">The in-process curl, which the script never runs.</param>
    /// <returns>The script's exit code.</returns>
    public async Task<int> RunAsync(UpstreamCurlInvocation invocation, Func<UpstreamCurlInvocation, Task<int>> runCurl)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        StringBuilder output = new();
        StringBuilder error = new();
        int exitCode = new ManualPageGenerator(ReadFile, PageDate(invocation.EnvironmentVariables))
            .Run(invocation.Arguments, invocation.EnvironmentVariables.GetValueOrDefault("CURL_MAKETGZ_VERSION"), output, error);
        await invocation.StandardOutput.WriteAsync(Encoding.Latin1.GetBytes(output.ToString()));
        await invocation.StandardError.WriteAsync(Encoding.Latin1.GetBytes(error.ToString()));
        return exitCode;
    }

    /// <summary>The page's date: <c>SOURCE_DATE_EPOCH</c> as a UTC date, else today's local date.</summary>
    /// <param name="environment">The invocation's environment.</param>
    /// <returns>The date, <c>yyyy-MM-dd</c>.</returns>
    public string PageDate(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        DateTimeOffset date = environment.TryGetValue("SOURCE_DATE_EPOCH", out string? epoch)
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(epoch, CultureInfo.InvariantCulture))
            : timeProvider.GetLocalNow();
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string? ReadFile(string path) => File.Exists(path) ? Encoding.Latin1.GetString(File.ReadAllBytes(path)) : null;
}
