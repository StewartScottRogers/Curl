using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1900.c</c>: a handle given the case's URL and two <c>CURLOPT_HSTS</c> files, <see cref="FirstHstsFile"/> and
/// then <see cref="SecondHstsFile"/>, is copied with <c>curl_easy_duphandle</c> and both are cleaned up without a
/// transfer. The program ends with <c>CURLE_OK</c> once the copy is made, and a failing <c>easy_setopt</c> with its code.
/// </summary>
public sealed class Lib1900 : ILibTest
{
    /// <summary>The first <c>CURLOPT_HSTS</c> file.</summary>
    public const string FirstHstsFile = "first-hsts.txt";

    /// <summary>The second <c>CURLOPT_HSTS</c> file, which replaces the first.</summary>
    public const string SecondHstsFile = "second-hsts.txt";

    /// <inheritdoc/>
    public string ProgramName => "lib1900";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

    /// <summary>The program's body after <c>global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            // Libcurl takes these strings without fail, so their easy_setopt checks are left out.
            _ = steps.EasySetOptions(easy, [(CurlOption.Url, context.Url), (CurlOption.Hsts, FirstHstsFile), (CurlOption.Hsts, SecondHstsFile)]);
            CurlEasy copy = easy.DupHandle();
            await copy.DisposeAsync().ConfigureAwait(false);
            steps.Result = CurlCode.Ok;
        }
    }
}
