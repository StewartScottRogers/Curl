using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1557.c</c>: a multi handle limited to one connection per host (<c>CURLMOPT_MAX_HOST_CONNECTIONS</c> 1) with
/// two easy handles for the same URL added, one <c>multi_perform</c>, then the second handle removed while it still
/// waits for a connection, and the first: removing a handle from the connect-pending list must leave no dangling
/// entry. Cleanup is the two easy handles, then the multi handle. The program ends with <c>CURLE_OK</c> unless a
/// step failed.
/// </summary>
public sealed class Lib1557 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1557";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            // Libcurl takes any count of 0 or more for CURLMOPT_MAX_HOST_CONNECTIONS, so multi_setopt cannot fail here.
            _ = multi.SetOption(CurlMultiOption.MaxHostConnections, 1L);
            if (!steps.EasyInit(library, out CurlEasy? first))
            {
                return;
            }

            await using (first.ConfigureAwait(false))
            {
                if (!steps.SetOptionsAndAddHandle(first, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url)], multi)
                    || !steps.EasyInit(library, out CurlEasy? second))
                {
                    return;
                }

                await using (second.ConfigureAwait(false))
                {
                    _ = steps.SetOptionsAndAddHandle(second, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url)], multi)
                        && (await steps.MultiPerformAsync(multi, cancellationToken).ConfigureAwait(false)).GoesOn
                        && steps.MultiRemoveHandle(multi, second)
                        && steps.MultiRemoveHandle(multi, first);
                }
            }
        }
    }
}
