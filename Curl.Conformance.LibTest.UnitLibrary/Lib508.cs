using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib508.c</c>: a POST of <c>this is what we post to the silly web server\n</c> with
/// <c>CURLOPT_POSTFIELDSIZE</c> 45, its body given through a read callback one byte at a time, verbose and
/// headers on.
/// </summary>
public sealed class Lib508 : ILibTest
{
    /// <summary>The bytes the program posts.</summary>
    public static ReadOnlyMemory<byte> PostData { get; } = Encoding.ASCII.GetBytes("this is what we post to the silly web server\n");

    /// <inheritdoc/>
    public string ProgramName => "lib508";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The program's body after <c>curl_global_init</c>.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
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
            var reader = new OneByteReader(PostData);
            await steps.SetOptionsAndPerformAsync(
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

    /// <summary><c>t508_read_cb</c>: hands over the data one byte a call, then 0.</summary>
    /// <param name="data">The bytes to hand over.</param>
    public sealed class OneByteReader(ReadOnlyMemory<byte> data)
    {
        private ReadOnlyMemory<byte> left = data;

        /// <summary>Copies one byte into <paramref name="buffer"/>.</summary>
        /// <param name="buffer">Where the byte goes.</param>
        /// <returns>1, or 0 when no data is left or the buffer is empty.</returns>
        public long Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || left.IsEmpty)
            {
                return 0;
            }

            buffer[0] = left.Span[0];
            left = left[1..];
            return 1;
        }
    }
}
