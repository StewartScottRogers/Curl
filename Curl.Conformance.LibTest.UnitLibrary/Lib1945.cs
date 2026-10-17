using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Headers;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1945.c</c>: a verbose GET of the case's URL following redirects, its body thrown away, through the HTTP proxy
/// <c>libtest_arg2</c> tunnelled when one is given; a failing perform writes <c>badness: &lt;code&gt;</c> on stdout, and
/// either way every header of the first request from the <c>CONNECT</c> response, the response, the trailers and the
/// 1xx responses is written as <c> name == value (index/amount)</c>, walked with <c>curl_easy_nextheader</c>. The
/// perform's code is the program's.
/// </summary>
public sealed class Lib1945 : ILibTest
{
    /// <summary>The origins the program walks: <c>CURLH_CONNECT | CURLH_HEADER | CURLH_TRAILER | CURLH_1XX</c>.</summary>
    public const CurlHeaderOrigin ShownOrigins =
        CurlHeaderOrigin.Connect | CurlHeaderOrigin.Header | CurlHeaderOrigin.Trailer | CurlHeaderOrigin.Informational;

    /// <inheritdoc/>
    public string ProgramName => "lib1945";

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
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            // Upstream sets these with plain curl_easy_setopt, its answers unchecked.
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            if (!string.IsNullOrEmpty(context.Argument2))
            {
                _ = easy.SetOption(CurlOption.Proxy, context.Argument2);
                _ = easy.SetOption(CurlOption.Httpproxytunnel, 1L);
            }

            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                output.Write($"badness: {(int)steps.Result}\n");
            }

            ShowHeaders(easy, output);
        }
    }

    /// <summary><c>t1945_showem</c>: writes every header of the first request from <see cref="ShownOrigins"/>.</summary>
    /// <param name="easy">The handle whose transfer is read.</param>
    /// <param name="output">The program's stdout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static void ShowHeaders(CurlEasy easy, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);
        CurlHeader? header = null;
        while ((header = easy.NextHeader(ShownOrigins, 0, header)) is not null)
        {
            output.Write($" {header.Name} == {header.Value} ({header.Index}/{header.Amount})\n");
        }
    }
}
