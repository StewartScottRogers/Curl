using System.Text;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib536.c</c>: <c>http://usingproxy.test/</c> through the proxy at the case's URL, with
/// <c>goingdirect.test</c> in <c>CURLOPT_NOPROXY</c> and <c>libtest_arg3</c> in <c>CURLOPT_RESOLVE</c>,
/// then, when that succeeds, <c>libtest_arg2</c> on the same handle; after each successful transfer
/// it prints <c>This used the proxy</c> or <c>This DID NOT use the proxy</c> from
/// <c>CURLINFO_USED_PROXY</c> to stdout.
/// </summary>
public sealed class Lib536 : ILibTest
{
    /// <summary>The URL the first transfer fetches through the proxy.</summary>
    public const string UrlWithProxy = "http://usingproxy.test/";

    /// <inheritdoc/>
    public string ProgramName => "lib536";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
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
            if (context.Argument3 is null)
            {
                return;
            }

            CurlSlist host = CurlSlist.Append(null, context.Argument3);
            bool first = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Resolve, host),
                    (CurlOption.Proxy, context.Url),
                    (CurlOption.Url, UrlWithProxy),
                    (CurlOption.Noproxy, "goingdirect.test"),
                    (CurlOption.Verbose, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
            if (first)
            {
                await WriteProxyStatusAsync(easy, context.StandardOutput, cancellationToken).ConfigureAwait(false);
                if (await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Url, context.Argument2)], cancellationToken).ConfigureAwait(false))
                {
                    await WriteProxyStatusAsync(easy, context.StandardOutput, cancellationToken).ConfigureAwait(false);
                }
            }

            CurlSlist.FreeAll(host);
        }
    }

    /// <summary>
    /// <c>proxystat</c>: whether the last transfer used the proxy. Upstream prints only when
    /// <c>CURLINFO_USED_PROXY</c> answers, which Libcurl's always does on a live handle.
    /// </summary>
    /// <param name="easy">The handle.</param>
    /// <param name="output">The program's stdout.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes when the line is written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask WriteProxyStatusAsync(CurlEasy easy, Stream output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(output);
        easy.GetInfo(CurlInfo.UsedProxy, out object? used);
        string line = used is long and not 0 ? "This used the proxy\n" : "This DID NOT use the proxy\n";
        await output.WriteAsync(Encoding.ASCII.GetBytes(line), cancellationToken).ConfigureAwait(false);
    }
}
