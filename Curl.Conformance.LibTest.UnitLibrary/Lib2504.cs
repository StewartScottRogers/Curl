using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib2504.c</c>: two transfers of the case's URL on one handle with the cookie engine on, the first with a
/// <c>Host: victim.internal</c> header and the second without, each followed by <c>reqN=code</c> and the cookie list
/// under <c>== after request N ==</c> on stdout. The second transfer's code is the program's. A failing
/// <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
public sealed class Lib2504 : ILibTest
{
    /// <summary>The header the first transfer sends.</summary>
    public const string HostHeader = "Host: victim.internal";

    /// <inheritdoc/>
    public string ProgramName => "lib2504";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
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
            // Libcurl's curl_slist_append and these easy_setopt calls never fail, so upstream's `if(hdrs)` and its
            // macros' jumps to test_cleanup are left out.
            CurlSlist headers = CurlSlist.Append(null, HostHeader);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)LibTestUtilities.ThrowawayWrite);
            _ = easy.SetOption(CurlOption.Cookiefile, string.Empty);
            _ = easy.SetOption(CurlOption.Httpheader, headers);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            await RequestAsync(context, steps, easy, 1, cancellationToken).ConfigureAwait(false);
            _ = easy.SetOption(CurlOption.Httpheader, null);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            await RequestAsync(context, steps, easy, 2, cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(headers);
        }
    }

    private static async ValueTask RequestAsync(LibTestContext context, LibTestSteps steps, CurlEasy easy, int request, CancellationToken cancellationToken)
    {
        steps.Result = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        var text = new StringBuilder($"req{request}={(int)steps.Result}\n== after request {request} ==\n");
        // Libcurl answers the cookie list, possibly empty, on any live handle, so upstream's getinfo error line never shows.
        _ = easy.GetInfo(CurlInfo.Cookielist, out object? cookies);
        foreach (string cookie in (IReadOnlyList<string>)cookies!)
        {
            _ = text.Append(cookie).Append('\n');
        }

        context.StandardOutput.Write(Encoding.UTF8.GetBytes(text.ToString()));
    }
}
