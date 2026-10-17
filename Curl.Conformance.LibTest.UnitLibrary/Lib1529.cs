using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1529.c</c>: a verbose GET through the HTTP proxy <c>libtest_arg2</c> of the URL with
/// <see cref="InjectedRequest"/> appended, a request smuggled in after a CR LF, which the transfer must refuse as a
/// malformed URL; the transfer's code is the program's. A failing <c>curl_global_init</c> or <c>curl_easy_init</c>
/// writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>. Upstream formats the URL into a 512-byte
/// buffer; a case's URL never comes near that, so it is not cut.
/// </summary>
public sealed class Lib1529 : ILibTest
{
    /// <summary>What <c>lib1529.c</c> appends to the URL.</summary>
    public const string InjectedRequest = " HTTP/1.1\r\nGET http://1529.com/1529";

    /// <inheritdoc/>
    public string ProgramName => "lib1529";

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
                    (CurlOption.Url, context.Url + InjectedRequest),
                    (CurlOption.Proxy, context.Argument2),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Proxytype, LibProxyTunnelUpload.ProxyHttp),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }
}
