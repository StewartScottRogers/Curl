using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1939.c</c>: an HTTPS GET of the case's URL through the HTTP proxy <c>libtest_arg2</c> driven by one
/// <c>curl_multi_perform</c>, enough to start the tunnel, then the easy handle cleaned up <em>before</em> the multi
/// handle, the order that once freed the connection twice. A failing <c>curl_multi_add_handle</c> or
/// <c>curl_multi_perform</c> writes <c>curl_multi_perform failed</c> on stderr. Libcurl's setters cannot fail on the
/// case's URL and proxy, so their checks are left out. The
/// program always ends with <c>CURLE_OK</c>, and a multi or easy handle that cannot be created skips the rest.
/// </summary>
public sealed class Lib1939 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1939";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, CurlLibrary library, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            return;
        }

        CurlMulti multi = library.MultiInit();
        await using (multi.ConfigureAwait(false))
        {
            CurlEasy easy = library.EasyInit();
            await using (easy.ConfigureAwait(false))
            {
                _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
                _ = easy.SetOption(CurlOption.Url, context.Url);
                _ = easy.SetOption(CurlOption.Proxy, context.Argument2);
                await StartAsync(context, multi, easy, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary><c>curl_multi_add_handle</c> and one <c>curl_multi_perform</c>, writing the failure line if either fails.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="multi">The multi handle.</param>
    /// <param name="easy">The easy handle.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes after the perform.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask StartAsync(LibTestContext context, CurlMulti multi, CurlEasy easy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(multi);
        CurlMultiCode code = multi.AddHandle(easy);
        if (code == CurlMultiCode.Ok)
        {
            (code, _) = await multi.PerformAsync(cancellationToken).ConfigureAwait(false);
        }

        if (code != CurlMultiCode.Ok)
        {
            context.StandardError.Write("curl_multi_perform failed\n");
        }
    }
}
