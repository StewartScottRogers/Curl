using System.Globalization;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Info;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1533.c</c>: four verbose 3-byte POSTs on one handle whose read callback pauses until the response's first
/// bytes arrive (<see cref="PausedPoster"/>), two without and two with <c>CURLOPT_KEEP_SENDING_ON_ERROR</c>; each
/// writes <c>&lt;description&gt;: expected: N connections; actual: N connections</c> on stderr, and only the last
/// may reuse the connection. All four as expected ends with <see cref="LibTestErrorCodes.Success"/>; a wrong count
/// with <see cref="LibTestErrorCodes.Failure"/>, a failing transfer with
/// <see cref="LibTestErrorCodes.MajorBad"/>. A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its
/// failure and ends with <see cref="LibTestErrorCodes.MajorBad"/>. The check of <c>curl_easy_getinfo</c> itself is left
/// out: Libcurl answers that info on a live handle without fail.
/// </summary>
public sealed class Lib1533 : ILibTest
{
    /// <summary>Each request's description and the new connections it must make, in order.</summary>
    public static readonly IReadOnlyList<(string Description, long Connections)> Requests =
    [
        ("First request without CURLOPT_KEEP_SENDING_ON_ERROR", 1),
        ("Second request without CURLOPT_KEEP_SENDING_ON_ERROR", 1),
        ("First request with CURLOPT_KEEP_SENDING_ON_ERROR", 1),
        ("Second request with CURLOPT_KEEP_SENDING_ON_ERROR", 0),
    ];

    /// <inheritdoc/>
    public string ProgramName => "lib1533";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>; it ends holding the program's code.</param>
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
            var poster = new PausedPoster(easy);
            _ = easy.SetOption(CurlOption.Url, context.Url);
            _ = easy.SetOption(CurlOption.Post, 1L);
            _ = easy.SetOption(CurlOption.PostfieldsizeLarge, (long)PausedPoster.BodyLength);
            _ = easy.SetOption(CurlOption.Verbose, 1L);
            _ = easy.SetOption(CurlOption.Readfunction, (ReadCallback)poster.Read);
            _ = easy.SetOption(CurlOption.Readdata, poster);
            _ = easy.SetOption(CurlOption.Writefunction, (WriteCallback)poster.Write);
            _ = easy.SetOption(CurlOption.Writedata, poster);
            steps.Result = LibTestErrorCodes.Success;
            for (int request = 0; request < Requests.Count && steps.Result == LibTestErrorCodes.Success; request++)
            {
                if (request == 2)
                {
                    _ = easy.SetOption(CurlOption.KeepSendingOnError, 1L);
                }

                poster.Reset();
                steps.Result = await PerformAndCheckConnectionsAsync(context, easy, Requests[request], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary><c>perform_and_check_connections</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="easy">The handle.</param>
    /// <param name="request">The request's description and the new connections it must make.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns><see cref="LibTestErrorCodes.Success"/>, <see cref="LibTestErrorCodes.Failure"/> or <see cref="LibTestErrorCodes.MajorBad"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask<CurlCode> PerformAndCheckConnectionsAsync(
        LibTestContext context,
        CurlEasy easy,
        (string Description, long Connections) request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(easy);
        CurlCode code = await easy.PerformAsync(cancellationToken).ConfigureAwait(false);
        if (code != CurlCode.Ok)
        {
            context.StandardError.Write($"curl_easy_perform() failed with {(int)code}\n");
            return LibTestErrorCodes.MajorBad;
        }

        easy.GetInfo(CurlInfo.NumConnects, out object? value);
        long connections = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        context.StandardError.Write($"{request.Description}: expected: {request.Connections} connections; actual: {connections} connections\n");
        return connections == request.Connections ? LibTestErrorCodes.Success : LibTestErrorCodes.Failure;
    }

    /// <summary><c>struct cb_data</c> with <c>t1533_read_cb</c> and <c>t1533_write_cb</c>.</summary>
    /// <param name="easy">The handle the write callback unpauses.</param>
    public sealed class PausedPoster(CurlEasy easy)
    {
        /// <summary>The bytes each request posts.</summary>
        public const int BodyLength = 3;

        private bool responseReceived;
        private bool paused;
        private int remainingBytes = BodyLength;

        /// <summary><c>reset_data</c>: nothing received, not paused, the whole body left to send.</summary>
        public void Reset()
        {
            responseReceived = false;
            paused = false;
            remainingBytes = BodyLength;
        }

        /// <summary>Pauses until the response arrives, then gives what is left of the body as <c>a</c>s.</summary>
        /// <param name="buffer">Where to write the bytes.</param>
        /// <returns>The bytes written, or <see cref="ReadCallbackResult.Pause"/>.</returns>
        public long Read(Span<byte> buffer)
        {
            if (!responseReceived)
            {
                paused = true;
                return ReadCallbackResult.Pause;
            }

            int count = Math.Min(remainingBytes, buffer.Length);
            buffer[..count].Fill((byte)'a');
            remainingBytes -= count;
            return count;
        }

        /// <summary>Marks the response received and, when the upload is paused, unpauses it; takes every byte.</summary>
        /// <param name="data">The bytes.</param>
        /// <returns>The byte count.</returns>
        public long Write(ReadOnlySpan<byte> data)
        {
            responseReceived = true;
            if (paused)
            {
                paused = false;
                _ = easy.Pause(CurlPause.Continue);
            }

            return data.Length;
        }
    }
}
