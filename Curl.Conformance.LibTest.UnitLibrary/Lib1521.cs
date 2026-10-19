using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Share;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1521.c</c>, which upstream generates with <c>mk-lib1521.pl</c> from <c>curl.h</c>: two easy handles
/// (<c>dep</c> and <c>curl</c>) and a share, then <see cref="Lib1521Checks"/>' <c>curl_easy_setopt</c> calls for every
/// option and <c>curl_easy_getinfo</c> calls for every info, one stdout line for each answer upstream does not
/// allow, then <c>curl_easy_setopt(curl, 1, 0L)</c> with its answer ignored, and <c>ok</c>.
/// </summary>
public sealed class Lib1521 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1521";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library));

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
        if (!steps.EasyInit(library, out CurlEasy? dependency))
        {
            return;
        }

        await using (dependency.ConfigureAwait(false))
        {
            if (!steps.EasyInit(library, out CurlEasy? easy))
            {
                return;
            }

            await using (easy.ConfigureAwait(false))
            {
                CurlShare share = library.ShareInit();
                using CurlUrlHandle url = library.UrlInit();
                using StreamWriter output = LibTestRun.StandardOutputWriter(context);
                new Lib1521Checks(easy.SetOption, info => easy.GetInfo(info, out _), new Lib1521Values(dependency, share, url), output).VerifyAll();
                _ = easy.SetOption((Libcurl.Options.CurlOption)1, 0L);
                _ = share.Cleanup();
                output.WriteLine("ok");
            }
        }
    }
}
