using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1647.c</c>: <c>--- First get 'URL'</c> on stdout and the case's URL with Digest proxy credentials from
/// <c>libtest_arg3</c>, then, unless it failed, <c>--- Then get 'ARG2'</c> and the same handle for <c>libtest_arg2</c> with
/// the credentials from <c>libtest_arg4</c>. The proxy comes from the environment; no certificate is verified. The last
/// perform's code is the program's.
/// </summary>
public sealed class Lib1647 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1647";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>res_global_init</c>.</summary>
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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"--- First get '{context.Url ?? "(nil)"}'\n");
            if (!await RunAsync(context, steps, easy, context.Url, context.Argument3, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            output.Write($"--- Then get '{context.Argument2 ?? "(nil)"}'\n");
            _ = await RunAsync(context, steps, easy, context.Argument2, context.Argument4, cancellationToken).ConfigureAwait(false);
        }
    }

    private static ValueTask<bool> RunAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, string? url, string? credentials, CancellationToken cancellationToken) =>
        steps.SetOptionsAndPerformAsync(
            easy,
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, url),
                (CurlOption.Proxyuserpwd, credentials),
                (CurlOption.Proxyauth, (long)HttpAuthenticationMethods.Digest),
                (CurlOption.SslVerifypeer, 0L),
                (CurlOption.SslVerifyhost, 0L),
                (CurlOption.ProxySslVerifypeer, 0L),
                (CurlOption.ProxySslVerifyhost, 0L),
                (CurlOption.Verbose, 1L),
            ],
            cancellationToken);
}
