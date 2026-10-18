using System.Globalization;
using System.Runtime.CompilerServices;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1532.c</c>: a GET whose <c>CURLINFO_RESPONSE_CODE</c> must be 200, and 0 again after
/// <c>curl_easy_reset</c> (github bug 1017). A failing transfer writes <c>&lt;file&gt;:&lt;line&gt;
/// curl_easy_perform() failed with code N (text)</c> on stderr and ends with its code; a wrong code writes
/// <c>unexpected response code N</c>, or the reset's two lines, and ends with <c>CURLE_HTTP_RETURNED_ERROR</c>. The
/// checks of <c>curl_easy_getinfo</c> itself are left out: Libcurl answers that info on a live handle without fail.
/// </summary>
public sealed class Lib1532 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1532";

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
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            if (!await steps.EasyPerformOrReportAsync(easy, cancellationToken, file, line).ConfigureAwait(false))
            {
                return;
            }

            string where = $"{Path.GetFileName(file)}:{line}";
            long code = ResponseCode(easy);
            if (code != 200)
            {
                context.StandardError.Write($"{where} unexpected response code {code}\n");
                steps.Result = Curl.Api.Codes.CurlCode.HttpReturnedError;
                return;
            }

            easy.Reset();
            if (ResponseCode(easy) != 0)
            {
                context.StandardError.Write($"{where} curl_easy_reset failed to zero the response code\npossible regression of github bug 1017\n");
                steps.Result = Curl.Api.Codes.CurlCode.HttpReturnedError;
            }
        }
    }

    /// <summary><c>CURLINFO_RESPONSE_CODE</c>.</summary>
    /// <param name="easy">The handle.</param>
    /// <returns>The last response's code, 0 for none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="easy"/> is <see langword="null"/>.</exception>
    public static long ResponseCode(CurlEasy easy)
    {
        ArgumentNullException.ThrowIfNull(easy);
        easy.GetInfo(CurlInfo.ResponseCode, out object? code);
        return Convert.ToInt64(code, CultureInfo.InvariantCulture);
    }
}
