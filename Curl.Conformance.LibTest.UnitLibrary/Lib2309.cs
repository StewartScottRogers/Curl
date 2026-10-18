using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2309.c</c>: a verbose handle for the case's URL through the proxy <c>libtest_arg3</c>, its <c>.netrc</c>
/// required from the file <c>libtest_arg2</c>, and a write callback answering <c>CURL_WRITEFUNC_ERROR</c>; the transfer
/// runs on a <c>curl_easy_duphandle</c> copy, then <c>Returned N, should be 23.</c> on stdout. The program ends with
/// <c>CURLE_OK</c> whatever the transfer gave.
/// </summary>
public sealed class Lib2309 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib2309";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(library);
        // Upstream's `if(curl)` and `if(curldupe)`: a library cleaned up gives no handle, and the program ends with
        // CURLE_OK; Libcurl's duphandle of a live handle always gives one.
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)Lib2308.FailWrite);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Proxy, context.Argument3);
            _ = easy.SetOption(CurlOption.Netrc, (long)NetrcUsage.Required);
            _ = easy.SetOption(CurlOption.NetrcFile, context.Argument2);
            CurlEasy dupe = easy.DupHandle();
            await using (dupe.ConfigureAwait(false))
            {
                await Lib2308.WriteReturnedAsync(context, dupe, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
