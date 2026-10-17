using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1916.c</c>: a post to the case's URL with empty <c>CURLOPT_POSTFIELDS</c>, or, for case 1917,
/// <c>CURLOPT_POST</c> and no post fields at all. A perform that fails writes <c>result: &lt;code&gt;</c> to stdout,
/// and its code is the program's; a <c>curl_global_init</c> that fails ends it with
/// <see cref="LibTestErrorCodes.MajorBad"/>, and an easy handle that cannot be created with <c>CURLE_OK</c>.
/// </summary>
public sealed class Lib1916 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1916";

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
        ArgumentNullException.ThrowIfNull(library);
        if (library.IsCleanedUp)
        {
            return;
        }

        CurlEasy easy = library.EasyInit();
        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = context.TestNumber == 1917 ? easy.SetOption(CurlOption.Post, 1L) : easy.SetOption(CurlOption.Postfields, string.Empty);
            if (!await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                using StreamWriter output = LibTestRun.StandardOutputWriter(context);
                output.Write($"result: {(int)steps.Result}\n");
            }
        }
    }
}
