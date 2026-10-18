using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3025.c</c>: a verbose transfer of the case's URL with its headers in the body and <c>ICY 200 OK</c> as a
/// <c>CURLOPT_HTTP200ALIASES</c> status line. The transfer's code is the program's. A failing <c>curl_global_init</c> or
/// <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib3025 : ILibTest
{
    /// <summary>The status line the transfer takes as <c>HTTP/1.0 200</c>.</summary>
    public const string Alias = "ICY 200 OK";

    /// <inheritdoc/>
    public string ProgramName => "lib3025";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
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
            // Libcurl's curl_slist_append and these easy_setopt calls never fail, so the macros' jumps to test_cleanup
            // are left out.
            CurlSlist aliases = CurlSlist.Append(null, Alias);
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Http200aliases, aliases);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Header, 1L);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(aliases);
        }
    }
}
