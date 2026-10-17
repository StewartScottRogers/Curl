using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1567.c</c>: a verbose GET that follows redirects, its URL handed over as a <c>CURLU</c> handle
/// (<c>CURLOPT_CURLU</c>), performed, then, unless it failed, <c>****************************** Do it again</c> on stderr
/// and the same handle performed again, so the second perform starts from the <c>CURLU</c>'s URL and not the one it
/// was redirected to. The last perform's code is the program's; an easy handle that cannot be created skips both and
/// ends with <c>CURLE_OK</c>. Libcurl's <c>curl_url()</c> cannot fail, so its <c>if(u)</c> is left out.
/// </summary>
public sealed class Lib1567 : ILibTest
{
    /// <summary>The line written on stderr between the two performs.</summary>
    public const string DoItAgainLine = "****************************** Do it again\n";

    /// <inheritdoc/>
    public string ProgramName => "lib1567";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using CurlUrlHandle url = library.UrlInit();
            _ = easy.SetOption(CurlOption.Writedata, context.StandardOutput);
            _ = easy.SetOption(CurlOption.Followlocation, 1L);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = url.Set(CurlUrlPart.Url, context.Url, CurlUrlFlags.None);
            _ = easy.SetOption(CurlOption.Curlu, url);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.Ok)
            {
                return;
            }

            context.StandardError.Write(DoItAgainLine);
            steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
