using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1571.c</c>, run by cases 1571 to 1575 and 1581: a verbose request with headers and the custom method
/// <c>IGLOO</c> that follows redirects, answering the perform's code. Cases 1571, 1572, 1575 and 1581 post <c>moo</c>,
/// 1581 keeps the <c>POST</c> after a 301 (<c>CURL_REDIR_POST_301</c>), and 1574 and 1575 follow with
/// <c>CURLFOLLOW_FIRSTONLY</c> (the custom method only on the first request) where the rest use
/// <c>CURLFOLLOW_OBEYCODE</c> (the method changed as the response code says).
/// </summary>
public sealed class Lib1571 : ILibTest
{
    private static readonly HashSet<int> PostingCases = [1571, 1572, 1575, 1581];

    private static readonly HashSet<int> FirstOnlyCases = [1574, 1575];

    /// <inheritdoc/>
    public string ProgramName => "lib1571";

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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = await steps.SetOptionsAndPerformAsync(easy, Options(context), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The options the program sets for its case, in its order.</summary>
    /// <param name="context">The program's context.</param>
    /// <returns>The options and their values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<(CurlOption Option, object? Value)> Options(LibTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        int testNumber = context.TestNumber;
        var options = new List<(CurlOption Option, object? Value)>
        {
            (CurlOption.Writedata, context.StandardOutput),
            (CurlOption.Header, 1L),
            (CurlOption.Verbose, 1L),
            (CurlOption.Url, context.Url),
        };
        if (PostingCases.Contains(testNumber))
        {
            options.Add((CurlOption.Postfields, "moo"));
        }

        if (testNumber == 1581)
        {
            options.Add((CurlOption.Postredir, (long)PostRedirect.Post301));
        }

        options.Add((CurlOption.Customrequest, "IGLOO"));
        options.Add((CurlOption.Followlocation, (long)(FirstOnlyCases.Contains(testNumber) ? FollowLocation.FirstOnly : FollowLocation.ObeyCode)));
        return options;
    }
}
