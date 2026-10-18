using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.ErrorText;
using Libcurl.Options;
using Libcurl.Share;

namespace Curl.Conformance.LibTest;

/// <summary>
/// The program <c>lib3103.c</c> and <c>lib3104.c</c> share: a share handle sharing cookies, and a verbose transfer with
/// headers of the program's <see cref="SiteUrl"/> through the case's URL as the proxy, with the cookie engine on and the
/// program's one cookie line (<see cref="CookieLine"/>) given to <c>CURLOPT_COOKIELIST</c>. A failing perform writes
/// <c>curl_easy_perform() failed: TEXT</c> on stderr; its code is the program's. Upstream checks neither
/// <c>curl_global_init</c>, <c>curl_share_init</c> nor <c>curl_easy_init</c>; a library that gives no handle ends the
/// program with <see cref="CurlCode.BadFunctionArgument"/>, as the first <c>easy_setopt</c> on no handle does.
/// </summary>
/// <param name="programNumber">The program's number, such as 3103.</param>
public abstract class LibSharedCookieThroughProxy(int programNumber) : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => $"lib{programNumber}";

    /// <summary>The URL the program asks the proxy for.</summary>
    public abstract string SiteUrl { get; }

    /// <summary>The cookie line the program gives <c>CURLOPT_COOKIELIST</c>.</summary>
    public abstract string CookieLine { get; }

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            steps.Result = CurlCode.BadFunctionArgument;
            return;
        }

        CurlShare share = library.ShareInit()!;
        _ = share.SetOption(CurlShareOption.Share, CurlLockData.Cookie);
        CurlEasy easy = library.EasyInit()!;
        await using (easy.ConfigureAwait(false))
        {
            (CurlOption, object?)[] options =
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Share, share),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
                (CurlOption.Proxy, context.Url),
                (CurlOption.Url, SiteUrl),
                (CurlOption.Cookiefile, string.Empty),
                (CurlOption.Cookielist, CookieLine),
            ];
            // Libcurl takes these easy_setopt values without fail, so the macros' jumps to test_cleanup are left out.
            LibTestRun.SetOptionsUnchecked(easy, options);
            if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                context.StandardError.Write($"curl_easy_perform() failed: {CurlErrorText.Easy(steps.Result)}\n");
            }
        }

        _ = share.Cleanup();
    }
}
