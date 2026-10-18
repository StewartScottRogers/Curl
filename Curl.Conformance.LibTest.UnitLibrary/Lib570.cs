using Curl.Api.Codes;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib570.c</c>: CSeq and session checks. A verbose RTSP <c>OPTIONS</c> must fail with
/// <see cref="CurlCode.RtspCseqError"/> (else <c>Failed to detect CSeq mismatch</c> and
/// <see cref="LibTestErrorCodes.MajorBad"/>); then, from client CSeq 999, a <c>SETUP</c> that must succeed and a
/// <c>PLAY</c> that must fail with <see cref="CurlCode.RtspSessionError"/>, which the program answers as success
/// (else <c>Failed to detect a Session ID mismatch</c> and <see cref="LibTestErrorCodes.Failure"/>). The checks of
/// <c>easy_setopt</c> calls that cannot fail are left out.
/// </summary>
public sealed class Lib570 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib570";

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
            steps.EasySetOptions(
                easy,
                [
                    (CurlOption.Headerdata, context.StandardOutput),
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.RtspRequest, (long)RtspRequest.Options),
                ]);
            var requests = new LibTestRtspRequests(steps, easy, context.Url);
            if (!requests.SetNextStreamUri())
            {
                return;
            }

            await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.RtspCseqError)
            {
                context.StandardError.Write("Failed to detect CSeq mismatch");
                steps.Result = LibTestErrorCodes.MajorBad;
                return;
            }

            steps.EasySetOptions(
                easy,
                [
                    (CurlOption.RtspClientCseq, 999L),
                    (CurlOption.RtspTransport, "RAW/RAW/UDP;unicast;client_port=3056-3057"),
                    (CurlOption.RtspRequest, (long)RtspRequest.Setup),
                ]);
            if (await requests.PerformNextAsync([], cancellationToken).ConfigureAwait(false))
            {
                await PlayAsync(context, steps, requests, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The <c>PLAY</c> that must fail with <see cref="CurlCode.RtspSessionError"/>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps.</param>
    /// <param name="requests">The program's requests.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask PlayAsync(LibTestContext context, LibTestSteps steps, LibTestRtspRequests requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(requests);
        if (!await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Play)], cancellationToken).ConfigureAwait(false)
            && steps.Result == CurlCode.RtspSessionError)
        {
            steps.Result = CurlCode.Ok;
            return;
        }

        context.StandardError.Write("Failed to detect a Session ID mismatch");
        steps.Result = LibTestErrorCodes.Failure;
    }
}
