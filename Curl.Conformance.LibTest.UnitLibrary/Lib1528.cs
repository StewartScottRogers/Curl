using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1528.c</c>: a verbose GET with headers in the output through the HTTP proxy <c>libtest_arg2</c>, sending
/// <c>User-Agent: Http Agent</c> to the server and <c>Proxy-User-Agent: Http Agent2</c> to the proxy, separate; the
/// transfer's code is the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure
/// and ends with <see cref="LibTestErrorCodes.MajorBad"/>. The <c>curl_slist_append</c> check is left out: Libcurl's
/// list append does not fail.
/// </summary>
public sealed class Lib1528 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1528";

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
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Proxy, context.Argument2),
                    (CurlOption.Httpheader, CurlSlist.Append(null, "User-Agent: Http Agent")),
                    (CurlOption.Proxyheader, CurlSlist.Append(null, "Proxy-User-Agent: Http Agent2")),
                    (CurlOption.Headeropt, LibProxyTunnelUpload.HeaderSeparate),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Proxytype, LibProxyTunnelUpload.ProxyHttp),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
