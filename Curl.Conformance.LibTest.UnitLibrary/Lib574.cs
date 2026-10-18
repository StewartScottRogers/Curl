using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib574.c</c>: an FTP wildcard download performed twice, with a <c>CURLOPT_FNMATCH_FUNCTION</c> that writes
/// <c>lib574: match string '…' against pattern '…'</c> to stderr and matches every file, and a timeout of
/// <c>TEST_HANG_TIMEOUT</c>; the checks of <c>easy_setopt</c> calls that cannot fail are left out. A failing transfer writes <c>curl_easy_perform() failed N</c> and is the answer.
/// </summary>
public sealed class Lib574 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib574";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            TextWriter error = context.StandardError;
            steps.EasySetOptions(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Wildcardmatch, 1L),
                    (CurlOption.FnmatchFunction, (FileNameMatchCallback)((pattern, name) => Match(error, pattern, name))),
                    (CurlOption.TimeoutMs, LibTestClock.TestHangTimeoutMilliseconds),
                ]);
            _ = await PerformOrReportAsync(steps, easy, error, cancellationToken).ConfigureAwait(false)
                && await PerformOrReportAsync(steps, easy, error, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>new_fnmatch</c>: writes the string and pattern to stderr and matches.</summary>
    /// <param name="error">The program's stderr.</param>
    /// <param name="pattern">The wildcard pattern.</param>
    /// <param name="name">The file name.</param>
    /// <returns><see cref="FileNameMatchResult.Match"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static FileNameMatchResult Match(TextWriter error, string pattern, string name)
    {
        ArgumentNullException.ThrowIfNull(error);
        error.Write($"lib574: match string '{name}' against pattern '{pattern}'\n");
        return FileNameMatchResult.Match;
    }

    private static async ValueTask<bool> PerformOrReportAsync(LibTestSteps steps, CurlEasy easy, TextWriter error, CancellationToken cancellationToken)
    {
        if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        error.Write($"curl_easy_perform() failed {(int)steps.Result}\n");
        return false;
    }
}
