using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1518.c</c> (cases 1518, 1519 and 1543): one GET, then <c>result</c>, <c>status</c> (the response code),
/// <c>redirects</c>, <c>effectiveurl</c> and <c>redirecturl</c> (<c>blank</c> for none) on stdout, one a line. Cases
/// 1518 and 1519 set the URL and turn redirects off; case 1543 hands it over as a URL handle parsed with
/// <c>CURLU_ALLOW_SPACE</c> and follows redirects, and a URL that does not parse ends the program at once with code 0.
/// A failing transfer ends it with its code and writes nothing. The program makes no <c>curl_global_init</c> call of
/// its own; it runs as <c>global_init</c>, which differs only with no engine. The <c>easy_setopt</c> checks are left
/// out: Libcurl takes these values without fail.
/// </summary>
public sealed class Lib1518 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1518";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
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
            using CurlUrlHandle url = library.UrlInit();
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            if (!SetUrl(context, easy, url)
                || !await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            _ = easy.GetInfo(CurlInfo.ResponseCode, out object? status);
            _ = easy.GetInfo(CurlInfo.RedirectCount, out object? redirects);
            _ = easy.GetInfo(CurlInfo.EffectiveUrl, out object? effectiveUrl);
            _ = easy.GetInfo(CurlInfo.RedirectUrl, out object? redirectUrl);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"result {(int)steps.Result}\nstatus {status}\nredirects {redirects}\neffectiveurl {effectiveUrl}\nredirecturl {redirectUrl ?? "blank"}\n");
        }
    }

    private static bool SetUrl(LibTestContext context, CurlEasy easy, CurlUrlHandle url)
    {
        if (context.TestNumber != 1543)
        {
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Followlocation, 0L);
            return true;
        }

        if (url.Set(CurlUrlPart.Url, context.Url, CurlUrlFlags.AllowSpace) != CurlUrlCode.Ok)
        {
            return false;
        }

        _ = easy.SetOption(CurlOption.Curlu, url);
        _ = easy.SetOption(CurlOption.Followlocation, 1L);
        return true;
    }
}
