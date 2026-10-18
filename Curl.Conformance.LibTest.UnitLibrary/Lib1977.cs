using Curl.Api.EngineContracts.Url;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;
using Libcurl.Url;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1977.c</c>: three transfers over <c>CURLOPT_CURLU</c>, each writing <c>effective URL: &lt;url&gt;</c> on stdout:
/// the case's URL in a first <c>CURLU</c>; the same URL with the query <c>foo</c> in a second, set as the handle's
/// <c>CURLU</c>; and that second <c>CURLU</c> with <c>bar</c> appended to its query (<c>CURLU_APPENDQUERY</c>) and not
/// set again, so the transfer must read the handle's current contents. A failing perform ends the program with its code.
/// </summary>
public sealed class Lib1977 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib1977";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithMacroLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>global_init</c>.</summary>
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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            using CurlUrlHandle first = library.UrlInit();
            using CurlUrlHandle second = library.UrlInit();
            using StreamWriter output = LibTestRun.StandardOutputWriter(context);
            _ = first.Set(CurlUrlPart.Url, context.Url, CurlUrlFlags.DefaultScheme);
            if (!await steps.SetOptionsAndPerformAsync(
                    easy,
                    [(CurlOption.Writedata, context.StandardOutput), (CurlOption.Curlu, first)],
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            WriteEffectiveUrl(easy, output);
            _ = second.Set(CurlUrlPart.Url, context.Url, CurlUrlFlags.DefaultScheme);
            _ = second.Set(CurlUrlPart.Query, "foo", CurlUrlFlags.None);
            if (!await steps.SetOptionsAndPerformAsync(easy, [(CurlOption.Curlu, second)], cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            WriteEffectiveUrl(easy, output);
            _ = second.Set(CurlUrlPart.Query, "bar", CurlUrlFlags.AppendQuery);
            if (await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false))
            {
                WriteEffectiveUrl(easy, output);
            }
        }
    }

    // Libcurl answers CURLINFO_EFFECTIVE_URL on a live handle without fail, so its checks are left out.
    private static void WriteEffectiveUrl(CurlEasy easy, StreamWriter output)
    {
        _ = easy.GetInfo(CurlInfo.EffectiveUrl, out object? effective);
        output.Write($"effective URL: {effective}\n");
    }
}
