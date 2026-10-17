using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib572.c</c>: RTSP <c>GET_PARAMETER</c> three ways on one verbose handle, each request to the case's URL and its
/// request number (<see cref="LibTestRtspRequests"/>): a <c>SETUP</c>, a <c>GET_PARAMETER</c> uploading the file
/// <c>libtest_arg2</c> names, a heartbeat <c>GET_PARAMETER</c> with no body, one posting <see cref="PostFields"/>, and
/// an <c>OPTIONS</c>. A file that cannot be opened answers <see cref="LibTestErrorCodes.MajorBad"/>; the program stops
/// at the first failing transfer and answers its code. The checks of <c>easy_setopt</c> calls that cannot fail are
/// left out.
/// </summary>
public sealed class Lib572 : ILibTest
{
    /// <summary>The fields the last <c>GET_PARAMETER</c> posts.</summary>
    public const string PostFields = "packets_received\njitter\n";

    /// <inheritdoc/>
    public string ProgramName => "lib572";

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
            steps.EasySetOptions(
                easy,
                [(CurlOption.Headerdata, context.StandardOutput), (CurlOption.Writedata, context.StandardOutput), (CurlOption.Verbose, 1L), (CurlOption.Url, context.Url)]);
            _ = await requests.PerformNextAsync(
                    [(CurlOption.RtspTransport, "Planes/Trains/Automobiles"), (CurlOption.RtspRequest, (long)RtspRequest.Setup)],
                    cancellationToken).ConfigureAwait(false)
                && await requests.UploadAsync(context.Argument2, context.StandardError, RtspRequest.GetParameter, [], cancellationToken).ConfigureAwait(false)
                && await requests.PerformNextAsync([], cancellationToken).ConfigureAwait(false)
                && await PostParametersAsync(steps, easy, requests, cancellationToken).ConfigureAwait(false)
                && await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Options)], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The <c>GET_PARAMETER</c> posting <see cref="PostFields"/>, the fields cleared again when it succeeds.</summary>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="requests">The program's requests.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> PostParametersAsync(LibTestSteps steps, CurlEasy easy, LibTestRtspRequests requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(requests);
        if (!await requests.PerformNextAsync(
            [(CurlOption.RtspRequest, (long)RtspRequest.GetParameter), (CurlOption.Postfields, PostFields)],
            cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        steps.EasySetOption(easy, CurlOption.Postfields, null);
        return true;
    }
}
