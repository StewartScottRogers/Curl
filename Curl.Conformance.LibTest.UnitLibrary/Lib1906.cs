using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1906.c</c>: the case's URL handed over as a <c>CURLU</c> handle (<c>CURLOPT_CURLU</c>, parsed with
/// <c>CURLU_DEFAULT_SCHEME</c>) with an error buffer, a 5-second connect timeout and <c>CURLOPT_PORT</c> 1, so the
/// verbose perform must fail with <c>CURLE_COULDNT_CONNECT</c> or <c>CURLE_OPERATION_TIMEDOUT</c>; any other answer
/// writes <c>failure expected, curl_easy_perform returned N: &lt;text&gt;, &lt;buffer&gt;</c> on stderr and ends the
/// program with it (<see cref="LibTestErrorCodes.MajorBad"/> for <c>CURLE_OK</c>). Then <c>curlu now: &lt;URL&gt;</c>, the
/// <c>CURLU</c>'s URL, which the port must not have changed, <c>CURLOPT_PORT</c> 0 and the perform again, a failure
/// writing <c>success expected, ...</c>, and <c>curlu now: &lt;URL&gt;</c> once more. The second perform's code is the
/// program's.
/// </summary>
public sealed class Lib1906 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1906";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after the library is initialized (upstream's <c>easy_init</c> does it implicitly).</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using CurlUrlHandle url = library.UrlInit();
            var errorBuffer = new StringBuilder();
            _ = url.Set(CurlUrlPart.Url, context.Url, CurlUrlFlags.DefaultScheme);
            if (!await steps.SetOptionsAndPerformAsync(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Curlu, url),
                        (CurlOption.Errorbuffer, errorBuffer),
                        (CurlOption.Verbose, 1L),
                        (CurlOption.ConnecttimeoutMs, 5000L),
                        (CurlOption.Port, 1L),
                    ],
                    cancellationToken).ConfigureAwait(false)
                && steps.Result is CurlCode.CouldntConnect or CurlCode.OperationTimedOut)
            {
                WriteUrl(context, url);
                _ = easy.SetOption(CurlOption.Port, 0L);
                if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
                {
                    WriteFailure(context, "success expected", steps.Result, errorBuffer);
                }

                WriteUrl(context, url);
                return;
            }

            WriteFailure(context, "failure expected", steps.Result, errorBuffer);
            steps.Result = steps.Result == CurlCode.Ok ? LibTestErrorCodes.MajorBad : steps.Result;
        }
    }

    private static void WriteFailure(LibTestContext context, string expectation, CurlCode result, StringBuilder errorBuffer) =>
        context.StandardError.Write($"{expectation}, curl_easy_perform returned {(int)result}: <{CurlErrorText.Easy(result)}>, <{errorBuffer}>\n");

    private static void WriteUrl(LibTestContext context, CurlUrlHandle url)
    {
        _ = url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? text);
        context.StandardError.Write($"curlu now: <{text}>\n");
    }
}
