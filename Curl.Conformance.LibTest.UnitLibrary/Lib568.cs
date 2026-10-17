using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib568.c</c>: four RTSP requests on one handle, each to the case's URL and its request number
/// (<see cref="LibTestRtspRequests"/>): a verbose <c>ANNOUNCE</c> uploading the file <c>libtest_arg2</c> names, a
/// <c>DESCRIBE</c>, an <c>ANNOUNCE</c> posting <see cref="PostFields"/> with the header
/// <c>Content-Type: posty goodness</c>, and an <c>OPTIONS</c>. A file that cannot be opened answers
/// <see cref="LibTestErrorCodes.MajorBad"/>; the program stops at the first failing transfer and answers its code.
/// The checks of <c>easy_setopt</c> calls that cannot fail are left out.
/// </summary>
public sealed class Lib568 : ILibTest
{
    /// <summary>The fields the second <c>ANNOUNCE</c> posts.</summary>
    public const string PostFields = "postyfield=postystuff&project=curl\n";

    /// <inheritdoc/>
    public string ProgramName => "lib568";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
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
            var requests = new LibTestRtspRequests(steps, easy, context.Url);
            steps.EasySetOptions(easy, [(CurlOption.Headerdata, context.StandardOutput), (CurlOption.Writedata, context.StandardOutput), (CurlOption.Url, context.Url)]);
            _ = await requests.UploadAsync(context.Argument2, context.StandardError, RtspRequest.Announce, [(CurlOption.Verbose, 1L)], cancellationToken).ConfigureAwait(false)
                && await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Describe)], cancellationToken).ConfigureAwait(false)
                && await AnnounceFieldsAsync(steps, easy, requests, cancellationToken).ConfigureAwait(false)
                && await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Options)], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The second <c>ANNOUNCE</c>: the next stream URI, then <see cref="PostFields"/> posted with a custom content type,
    /// both cleared again when it succeeds.
    /// </summary>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="requests">The program's requests.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> AnnounceFieldsAsync(LibTestSteps steps, CurlEasy easy, LibTestRtspRequests requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(requests);
        if (!requests.SetNextStreamUri())
        {
            return false;
        }

        CurlSlist headers = CurlSlist.Append(null, "Content-Type: posty goodness");
        bool goesOn = await steps.SetOptionsAndPerformAsync(
            easy,
            [(CurlOption.Rtspheader, headers), (CurlOption.RtspRequest, (long)RtspRequest.Announce), (CurlOption.Postfields, PostFields)],
            cancellationToken).ConfigureAwait(false);
        if (goesOn)
        {
            steps.EasySetOptions(easy, [(CurlOption.Postfields, null), (CurlOption.Rtspheader, null)]);
        }

        CurlSlist.FreeAll(headers);
        return goesOn;
    }
}
