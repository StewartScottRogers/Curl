using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1517.c</c>: a verbose POST with headers of <see cref="PostData"/>, its size set with
/// <c>CURLOPT_POSTFIELDSIZE</c>, through a read callback that waits a second before each call
/// (<see cref="SlowReader"/>), so the server answers before the body is sent; the transfer's code is the program's.
/// A failing <c>curl_global_init</c> or <c>curl_easy_init</c> writes its failure and ends with
/// <see cref="LibTestErrorCodes.MajorBad"/>.
/// </summary>
/// <param name="timeProvider">Where the read callback's wait is measured from.</param>
public sealed class Lib1517(TimeProvider timeProvider) : ILibTest
{
    /// <summary>The bytes the program posts.</summary>
    public static readonly byte[] PostData = "this is what we post to the silly web server\n"u8.ToArray();

    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc/>
    public string ProgramName => "lib1517";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken)
    {
        var clock = new LibTestClock(timeProvider);
        return LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, clock, cancellationToken));
    }

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="clock">The clock the read callback waits on.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes when the program reaches <c>curl_global_cleanup</c>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static async ValueTask TestAsync(LibTestContext context, LibTestSteps steps, CurlLibrary library, LibTestClock clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(clock);
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var reader = new SlowReader(PostData, () => clock.Wait(1000));
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Post, 1L),
                    (CurlOption.Postfieldsize, (long)PostData.Length),
                    (CurlOption.Readfunction, (ReadCallback)reader.Read),
                    (CurlOption.Readdata, reader),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t1517_read_cb</c>: waits, then copies as much of the data as fits, then 0.</summary>
    /// <param name="data">The bytes to give.</param>
    /// <param name="wait">The wait before each call, <c>curlx_wait_ms(1000)</c>.</param>
    public sealed class SlowReader(byte[] data, Action wait)
    {
        private int offset;

        /// <summary>Waits, then copies the next bytes that fit.</summary>
        /// <param name="buffer">Where to copy them.</param>
        /// <returns>The bytes copied; 0 at the end or for an empty buffer.</returns>
        public long Read(Span<byte> buffer)
        {
            wait();
            int given = Math.Min(buffer.Length, data.Length - offset);
            data.AsSpan(offset, given).CopyTo(buffer);
            offset += given;
            return given;
        }
    }
}
