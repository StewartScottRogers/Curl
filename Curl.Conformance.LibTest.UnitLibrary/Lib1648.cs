using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1648.c</c>: <see cref="Target"/> through the proxy <c>firstproxy</c> with the proxy credentials
/// <c>victim:secret</c>, then, unless it failed, with the credentials cleared through <c>secondproxy</c>, each request
/// announced on stdout. Both proxy names resolve (<c>CURLOPT_RESOLVE</c>) to the program's first argument (its
/// <c>hostip</c>) on port <c>libtest_arg2</c>. The last perform's code is the program's.
/// </summary>
public sealed class Lib1648 : ILibTest
{
    /// <summary>The URL fetched both times, <c>GET_THIS</c>.</summary>
    public const string Target = "http://example.com/";

    /// <inheritdoc/>
    public string ProgramName => "lib1648";

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
            string address = context.Url ?? "(nil)";
            string port = context.Argument2 ?? "(nil)";
            CurlSlist hosts = CurlSlist.Append(CurlSlist.Append(null, $"firstproxy:{port}:{address}"), $"secondproxy:{port}:{address}");
            // Libcurl cannot fail CURLOPT_RESOLVE or CURLOPT_PROXYUSERPWD on a live handle, so their easy_setopt checks are left out.
            _ = steps.EasySetOptions(easy, [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Resolve, hosts), (CurlOption.Proxyuserpwd, "victim:secret")]);
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            output.Write($"--- First get over firstproxy:{port}\n");
            if (!await RunAsync(steps, easy, $"firstproxy:{port}", cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            _ = steps.EasySetOption(easy, CurlOption.Proxyuserpwd, null);
            output.Write($"--- Then over 'secondproxy:{port}'\n");
            _ = await RunAsync(steps, easy, $"secondproxy:{port}", cancellationToken).ConfigureAwait(false);
        }
    }

    private static ValueTask<bool> RunAsync(LibTestSteps steps, CurlEasy easy, string proxy, CancellationToken cancellationToken) =>
        steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Url, Target), (CurlOption.Proxy, proxy), (CurlOption.Verbose, 1L)], cancellationToken);
}
