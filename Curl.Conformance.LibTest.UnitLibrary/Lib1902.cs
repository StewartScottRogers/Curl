using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1902.c</c>: <c>argv[1]</c> is a cookie file, set as both <c>CURLOPT_COOKIEFILE</c> and <c>CURLOPT_COOKIEJAR</c> on
/// a handle that is cleaned up without a transfer, which must leave the file as it was. A failing <c>easy_setopt</c>
/// ends the program with its code; an easy handle that cannot be created ends with <c>CURLE_OK</c>.
/// </summary>
public sealed class Lib1902 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1902";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = steps.EasySetOptions(easy, [(CurlOption.Cookiefile, context.Url), (CurlOption.Cookiejar, context.Url)]);
        }
    }
}
