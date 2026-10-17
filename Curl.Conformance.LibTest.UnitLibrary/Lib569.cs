using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options.Protocols;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib569.c</c>: session ID capture. The file <c>libtest_arg2</c> names is created first (<c>Could not open the
/// Session ID File</c> and <see cref="LibTestErrorCodes.MajorBad"/> when it cannot be). A verbose RTSP <c>SETUP</c>
/// with no transport must fail with <see cref="CurlCode.BadFunctionArgument"/>; then three times a <c>SETUP</c> with a
/// transport, whose <c>CURLINFO_RTSP_SESSION_ID</c> is written to the file as <c>Got Session ID: [id]</c>, and a
/// <c>TEARDOWN</c>, clearing the session ID after each round. The checks of <c>easy_setopt</c> calls that cannot fail
/// are left out.
/// </summary>
public sealed class Lib569 : ILibTest
{
    /// <inheritdoc/>
    public string ProgramName => "lib569";

    /// <inheritdoc/>
    public async Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        FileStream? idFile = LibTestRtspRequests.CreateOutput(context.Argument2);
        if (idFile is null)
        {
            context.StandardError.Write("Could not open the Session ID File\n");
            return LibTestResult.FromCurlCode(LibTestErrorCodes.MajorBad);
        }

        await using (idFile.ConfigureAwait(false))
        {
            return await LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, idFile, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="idFile">The open session ID file.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>A task that completes when the program reaches its cleanup.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, Stream idFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(idFile);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Headerdata, context.StandardOutput),
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Url, context.Url),
                    (CurlOption.RtspRequest, (long)RtspRequest.Setup),
                ],
                cancellationToken).ConfigureAwait(false);
            if (steps.Result != CurlCode.BadFunctionArgument)
            {
                context.StandardError.Write("This should have failed. Cannot setup without a Transport: header");
                steps.Result = LibTestErrorCodes.MajorBad;
                return;
            }

            var requests = new LibTestRtspRequests(steps, easy, context.Url);
            for (int round = 0; round < 3; round++)
            {
                if (!await SessionRoundAsync(steps, easy, requests, idFile, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
    }

    /// <summary>One round: a <c>SETUP</c>, its session ID written to <paramref name="idFile"/>, a <c>TEARDOWN</c>, and the session ID cleared.</summary>
    /// <param name="steps">The program's steps.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="requests">The program's requests.</param>
    /// <param name="idFile">The session ID file.</param>
    /// <param name="cancellationToken">Stops the transfers.</param>
    /// <returns>Whether the program goes on.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<bool> SessionRoundAsync(LibTestSteps steps, CurlEasy easy, LibTestRtspRequests requests, Stream idFile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(easy);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(idFile);
        if (!await requests.PerformNextAsync(
            [(CurlOption.RtspRequest, (long)RtspRequest.Setup), (CurlOption.RtspTransport, "Fake/NotReal/JustATest;foo=baz")],
            cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        easy.GetInfo(CurlInfo.RtspSessionId, out object? sessionId);
        await idFile.WriteAsync(Encoding.UTF8.GetBytes($"Got Session ID: [{sessionId as string ?? "(nil)"}]\n"), cancellationToken).ConfigureAwait(false);
        if (!await requests.PerformNextAsync([(CurlOption.RtspRequest, (long)RtspRequest.Teardown)], cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        steps.EasySetOption(easy, CurlOption.RtspSessionId, null);
        return true;
    }
}
