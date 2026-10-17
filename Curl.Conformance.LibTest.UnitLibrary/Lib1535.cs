using System.Globalization;
using System.Runtime.CompilerServices;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1535.c</c>: <c>CURLINFO_PROTOCOL</c> must be 0 on a new handle, <c>CURLPROTO_HTTP</c> (1) after a GET, and
/// 0 again on a <c>curl_easy_duphandle</c> copy and after <c>curl_easy_reset</c>. A wrong 0 writes
/// <c>&lt;file&gt;:&lt;line&gt; protocol init failed; expected 0 but is N</c> and ends with <c>CURLE_FAILED_INIT</c>,
/// a wrong 1 <c>protocol of http resource is incorrect; expected 1 but is N</c> and
/// <c>CURLE_HTTP_RETURNED_ERROR</c>, a failing transfer its <c>curl_easy_perform() failed</c> line and its code.
/// The checks of <c>curl_easy_getinfo</c> and <c>curl_easy_duphandle</c> themselves are left out: Libcurl answers
/// that info on a live handle, and copies one, without fail.
/// </summary>
public sealed class Lib1535 : ILibTest
{
    /// <summary><c>CURLPROTO_HTTP</c>.</summary>
    public const long ProtocolHttp = 1;

    /// <inheritdoc/>
    public string ProgramName => "lib1535";

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
            if (!ProtocolIs(context, steps, easy, 0, where))
            {
                return;
            }

            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            if (!await steps.EasyPerformOrReportAsync(easy, cancellationToken, file, line).ConfigureAwait(false)
                || !ProtocolIs(context, steps, easy, ProtocolHttp, where))
            {
                return;
            }

            CurlEasy dupe = easy.DupHandle();
            await using (dupe.ConfigureAwait(false))
            {
                if (!ProtocolIs(context, steps, dupe, 0, where))
                {
                    return;
                }
            }

            easy.Reset();
            _ = ProtocolIs(context, steps, easy, 0, where);
        }
    }

    /// <summary>Checks <c>CURLINFO_PROTOCOL</c>, writing upstream's line and setting its code when it is wrong.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="expected">The protocol it must be: 0 for none, or <see cref="ProtocolHttp"/>.</param>
    /// <param name="where">Upstream's <c>__FILE__:__LINE__</c>.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool ProtocolIs(LibTestContext context, LibTestSteps steps, CurlEasy easy, long expected, string where)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(easy);
        easy.GetInfo(CurlInfo.Protocol, out object? value);
        long protocol = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        if (protocol == expected)
        {
            return true;
        }

        if (expected == 0)
        {
            context.StandardError.Write($"{where} protocol init failed; expected 0 but is {protocol}\n");
            steps.Result = CurlCode.FailedInit;
        }
        else
        {
            context.StandardError.Write($"{where} protocol of http resource is incorrect; expected {ProtocolHttp} but is {protocol}\n");
            steps.Result = CurlCode.HttpReturnedError;
        }

        return false;
    }
}
