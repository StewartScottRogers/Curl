using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1514.c</c>: a POST of <c>dummy</c> through a read callback that gives one byte a call
/// (<see cref="OneByteReader"/>), with no <c>CURLOPT_POSTFIELDSIZE</c>, so the size is unknown; for case 1539 it
/// speaks HTTP/1.0 (<c>CURLOPT_HTTP_VERSION</c>), where no chunked upload is possible. The transfer's code is the
/// program's.
/// </summary>
public sealed class Lib1514 : ILibTest
{
    /// <summary><c>CURL_HTTP_VERSION_1_0</c>.</summary>
    public const long HttpVersion10 = 1;

    /// <inheritdoc/>
    public string ProgramName => "lib1514";

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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var reader = new OneByteReader("dummy"u8.ToArray());
            List<(CurlOption Option, object? Value)> options =
            [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Url, context.Url),
                (CurlOption.Post, 1L),
                (CurlOption.Readfunction, (ReadCallback)reader.Read),
                (CurlOption.Readdata, reader),
            ];
            if (context.TestNumber == 1539)
            {
                options.Add((CurlOption.HttpVersion, HttpVersion10));
            }

            _ = await steps.SetOptionsAndPerformAsync(easy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t1514_read_cb</c>: gives the data one byte a call, then 0.</summary>
    /// <param name="data">The bytes to give.</param>
    public sealed class OneByteReader(byte[] data)
    {
        private int offset;

        /// <summary>Copies the next byte, if any is left and the buffer has room.</summary>
        /// <param name="buffer">Where to copy it.</param>
        /// <returns>1 for a byte copied, 0 at the end or for an empty buffer.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || offset == data.Length)
            {
                return 0;
            }

            buffer[0] = data[offset++];
            return 1;
        }
    }
}
