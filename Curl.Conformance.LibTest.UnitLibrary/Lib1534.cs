using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1534.c</c>: <c>CURLINFO_FILETIME</c> must be -1 on a new handle, 30 after a GET with
/// <c>CURLOPT_FILETIME</c>, and -1 again on a <c>curl_easy_duphandle</c> copy and after <c>curl_easy_reset</c>. A
/// wrong -1 writes <c>&lt;file&gt;:&lt;line&gt; filetime init failed; expected -1 but is N</c> and ends with
/// <c>CURLE_FAILED_INIT</c>, a wrong 30 <c>filetime of http resource is incorrect; expected 30 but is N</c> and
/// <c>CURLE_HTTP_RETURNED_ERROR</c>, a failing transfer its <c>curl_easy_perform() failed</c> line and its code.
/// The checks of <c>curl_easy_getinfo</c> and <c>curl_easy_duphandle</c> themselves are left out: Libcurl answers
/// that info on a live handle, and copies one, without fail.
/// </summary>
public sealed class Lib1534 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1534";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <param name="file">The calling source file, upstream's <c>__FILE__</c>.</param>
    /// <param name="line">The calling line, upstream's <c>__LINE__</c>.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(
        LibTestContext context,
        LibTestSteps steps,
        CurlLibrary library,
        CancellationToken cancellationToken,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            string where = $"{Path.GetFileName(file)}:{line}";
            if (!FiletimeIs(context, steps, easy, -1, where, CurlCode.FailedInit))
            {
                return;
            }

            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Filetime, 1L);
            if (!await steps.EasyPerformOrReportAsync(easy, cancellationToken, file, line).ConfigureAwait(false)
                || !FiletimeIs(context, steps, easy, 30, where, CurlCode.HttpReturnedError))
            {
                return;
            }

            CurlEasy dupe = easy.DupHandle();
            await using (dupe.ConfigureAwait(false))
            {
                if (!FiletimeIs(context, steps, dupe, -1, where, CurlCode.FailedInit))
                {
                    return;
                }
            }

            easy.Reset();
            _ = FiletimeIs(context, steps, easy, -1, where, CurlCode.FailedInit);
        }
    }

    /// <summary>Checks <c>CURLINFO_FILETIME</c>, writing upstream's line and setting <paramref name="failure"/> when it is wrong.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="expected">The time it must be: -1 for none, or 30.</param>
    /// <param name="where">Upstream's <c>__FILE__:__LINE__</c>.</param>
    /// <param name="failure">The code a wrong time ends the program with.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool FiletimeIs(LibTestContext context, LibTestSteps steps, CurlEasy easy, long expected, string where, CurlCode failure)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(easy);
        easy.GetInfo(CurlInfo.Filetime, out object? value);
        long filetime = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        if (filetime == expected)
        {
            return true;
        }

        string message = expected == -1
            ? $"filetime init failed; expected -1 but is {filetime}"
            : $"filetime of http resource is incorrect; expected 30 but is {filetime}";
        context.StandardError.Write($"{where} {message}\n");
        steps.Result = failure;
        return false;
    }
}
