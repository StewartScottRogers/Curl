using Libcurl.Global;
using Libcurl.Multi;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1550.c</c>: <c>CURLMOPT_PIPELINING_SERVER_BL</c> and <c>CURLMOPT_PIPELINING_SITE_BL</c> set on a multi handle,
/// their answers ignored, then the handle cleaned up: setting the long-ignored blocklists must not fail or crash. The
/// program ends with <c>CURLE_OK</c> unless <c>global_init</c> or the multi handle's init fails.
/// </summary>
public sealed class Lib1550 : ILibTest
{
    /// <summary>The server blocklist, <c>bl_servers</c> without its <c>NULL</c> end.</summary>
    public static IReadOnlyList<string> BlockedServers { get; } = ["Microsoft-IIS/6.0", "nginx/0.8.54"];

    /// <summary>The site blocklist, <c>bl_sites</c> without its <c>NULL</c> end.</summary>
    public static IReadOnlyList<string> BlockedSites { get; } = ["curl.se:443", "example.com:80"];

    /// <inheritdoc/>
    public string ProgramName => "lib1550";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(steps, library));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.MultiInit(library, out CurlMulti? multi))
        {
            return;
        }

        await using (multi.ConfigureAwait(false))
        {
            _ = multi.SetOption(CurlMultiOption.PipeliningServerBlocklist, BlockedServers);
            _ = multi.SetOption(CurlMultiOption.PipeliningSiteBlocklist, BlockedSites);
        }
    }
}
