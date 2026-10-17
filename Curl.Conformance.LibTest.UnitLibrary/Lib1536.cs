using System.Runtime.CompilerServices;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1536.c</c>: <c>CURLINFO_SCHEME</c> must be <c>NULL</c> on a new handle, <c>http</c> after a GET, and
/// <c>NULL</c> again on a <c>curl_easy_duphandle</c> copy and after <c>curl_easy_reset</c>. A scheme where none
/// belongs writes <c>&lt;file&gt;:&lt;line&gt; scheme init failed; expected NULL</c> and ends with
/// <c>CURLE_FAILED_INIT</c>, a missing or wrong <c>http</c>
/// <c>scheme of http resource is incorrect; expected 'http' but is NULL</c> (or <c>invalid</c>) and
/// <c>CURLE_HTTP_RETURNED_ERROR</c>, a failing transfer its <c>curl_easy_perform() failed</c> line and its code.
/// The checks of <c>curl_easy_getinfo</c> and <c>curl_easy_duphandle</c> themselves are left out: Libcurl answers
/// that info on a live handle, and copies one, without fail.
/// </summary>
public sealed class Lib1536 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1536";

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
            if (!SchemeIs(context, steps, easy, null, where))
            {
                return;
            }

            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            if (!await steps.EasyPerformOrReportAsync(easy, cancellationToken, file, line).ConfigureAwait(false)
                || !SchemeIs(context, steps, easy, "http", where))
            {
                return;
            }

            CurlEasy dupe = easy.DupHandle();
            await using (dupe.ConfigureAwait(false))
            {
                if (!SchemeIs(context, steps, dupe, null, where))
                {
                    return;
                }
            }

            easy.Reset();
            _ = SchemeIs(context, steps, easy, null, where);
        }
    }

    /// <summary>Checks <c>CURLINFO_SCHEME</c>, writing upstream's line and setting its code when it is wrong.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="expected">The scheme it must be: <see langword="null"/> for none, or <c>http</c>.</param>
    /// <param name="where">Upstream's <c>__FILE__:__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool SchemeIs(LibTestContext context, LibTestSteps steps, CurlEasy easy, string? expected, string where)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(easy);
        easy.GetInfo(CurlInfo.Scheme, out object? value);
        string? scheme = value as string;
        if (string.Equals(scheme, expected, StringComparison.Ordinal))
        {
            return true;
        }

        if (expected is null)
        {
            context.StandardError.Write($"{where} scheme init failed; expected NULL\n");
            steps.Result = CurlCode.FailedInit;
        }
        else
        {
            context.StandardError.Write($"{where} scheme of http resource is incorrect; expected 'http' but is {(scheme is null ? "NULL" : "invalid")}\n");
            steps.Result = CurlCode.HttpReturnedError;
        }

        return false;
    }
}
