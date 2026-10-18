using System.Globalization;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Multi;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Multi;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib3105.c</c>: a handle added to a multi handle and then removed from it twice; both removals must answer
/// <c>CURLM_OK</c>. A removal that answers anything else writes <c>N was unexpected</c> on stderr and ends the program
/// with <see cref="CurlCode.FailedInit"/>. A failing step ends it with that step's code.
/// </summary>
public sealed class Lib3105 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib3105";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The program's body after <c>global_init</c>; its <c>i</c> is <see cref="LibTestSteps.Result"/> when no step failed.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                // Libcurl takes any URL string without fail, so the easy_setopt macro's jump to test_cleanup is left out.
                _ = easy.SetOption(CurlOption.Url, context.Url);
                if (steps.MultiAddHandle(multi, easy))
                {
                    RemoveTwice(context, steps, multi, easy);
                }

                // Cleanup type MU: the multi handle goes before the easy handle.
                await multi.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static void RemoveTwice(LibTestContext context, LibTestSteps steps, CurlMulti multi, CurlEasy easy)
    {
        CurlMultiCode code = multi.RemoveHandle(easy);
        if (code == CurlMultiCode.Ok)
        {
            code = multi.RemoveHandle(easy);
        }

        if (code != CurlMultiCode.Ok)
        {
            context.StandardError.Write(string.Create(CultureInfo.InvariantCulture, $"{(int)code} was unexpected\n"));
            steps.Result = CurlCode.FailedInit;
        }
    }
}
