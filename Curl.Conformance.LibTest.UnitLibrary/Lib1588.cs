using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1588.c</c>: the case's URL through the proxy <c>firstproxy</c> with Digest proxy credentials from
/// <c>libtest_arg4</c>, then, unless it failed, <c>lib1588: now we do the request again</c> on stderr and the same handle
/// through <c>secondproxy</c>. Both proxy names resolve (<c>CURLOPT_RESOLVE</c>) to <c>libtest_arg2</c> on port
/// <c>libtest_arg3</c>. Fewer than two arguments after the URL end with <see cref="LibTestErrorCodes.MajorBad"/>
/// before <c>curl_global_init</c>; the last perform's code is the program's.
/// </summary>
public sealed class Lib1588 : ILibTest
{
    /// <summary>The line written on stderr between the two requests.</summary>
    public const string AgainLine = "lib1588: now we do the request again\n";

    /// <inheritdoc/>
    public string ProgramName => "lib1588";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Arguments.Count < 2
            ? Task.FromResult(LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad))
            : LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));
    }

    /// <summary>The program's body after <c>res_global_init</c>.</summary>
    /// <param name="context">The program's context, with at least two arguments after the URL.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
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
            string address = context.Argument2!;
            string port = context.Argument3 ?? "(nil)";
            CurlSlist hosts = CurlSlist.Append(CurlSlist.Append(null, $"firstproxy:{port}:{address}"), $"secondproxy:{port}:{address}");
            // Libcurl cannot fail CURLOPT_RESOLVE on a live handle, so its easy_setopt check is left out.
            _ = easy.SetOption(CurlOption.Resolve, hosts);
            if (!await RunAsync(context, steps, easy, $"firstproxy:{port}", cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            context.StandardError.Write(AgainLine);
            _ = await RunAsync(context, steps, easy, $"secondproxy:{port}", cancellationToken).ConfigureAwait(false);
        }
    }

    private static ValueTask<bool> RunAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, string proxy, CancellationToken cancellationToken) =>
        steps.SetOptionsAndPerformAsync(
            easy,
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Proxy, proxy),
                (CurlOption.Proxyuserpwd, context.Argument4),
                (CurlOption.Proxyauth, (long)HttpAuthenticationMethods.Digest),
                (CurlOption.Verbose, 1L),
                (CurlOption.Header, 1L),
            ],
            cancellationToken);
}
