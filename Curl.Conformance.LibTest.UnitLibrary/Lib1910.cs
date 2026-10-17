using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1910.c</c>: a GET of the case's URL following redirects with <c>CURLOPT_USERPWD</c> <see cref="UserPassword"/>,
/// whose user name and password each hold a newline. The perform's code is the program's, and
/// <see cref="LibTestErrorCodes.MajorBad"/> when no easy handle can be created. <c>start_test_timing</c> is left out:
/// the program never checks the test's time.
/// </summary>
public sealed class Lib1910 : ILibTest
{
    /// <summary>The credentials, <c>user\nname:pass\nword</c>.</summary>
    public const string UserPassword = "user\nname:pass\nword";

    /// <inheritdoc/>
    public string ProgramName => "lib1910";

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
        steps.Result = LibTestErrorCodes.MajorBad;
        if (library.IsCleanedUp)
        {
            return;
        }

        CurlEasy easy = library.EasyInit();
        await using (easy.ConfigureAwait(false))
        {
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Noprogress, 1L);
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Userpwd, UserPassword);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
        }
    }
}
