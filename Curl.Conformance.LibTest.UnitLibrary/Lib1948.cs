using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1948.c</c>: a <c>PUT</c> of <see cref="PutData"/> to the case's URL through a <c>CURLOPT_READFUNCTION</c>,
/// headers in the output, then, unless it failed, the same handle switched to <c>CURLOPT_POST</c> with the same bytes
/// as <c>CURLOPT_POSTFIELDS</c>: the post must win over the upload. The last perform's code is the program's.
/// </summary>
public sealed class Lib1948 : ILibTest
{
    /// <summary><c>testput</c>, the bytes both requests send.</summary>
    public const string PutData = "This is test PUT data\n";

    /// <inheritdoc/>
    public string ProgramName => "lib1948";

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
        if (!steps.EasyInit(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            var buffer = new PutBuffer(System.Text.Encoding.ASCII.GetBytes(PutData));
            if (await steps.SetOptionsAndPerformAsync(
                    easy,
                    [
                        (CurlOption.Writedata, context.StandardOutput),
                        (CurlOption.Upload, 1L),
                        (CurlOption.Header, 1L),
                        (CurlOption.Readfunction, (ReadCallback)buffer.Read),
                        (CurlOption.Readdata, buffer),
                        (CurlOption.Infilesize, (long)PutData.Length),
                        (CurlOption.Url, context.Url),
                    ],
                    cancellationToken).ConfigureAwait(false))
            {
                _ = await steps.SetOptionsAndPerformAsync(
                    easy,
                    [(CurlOption.Post, 1L), (CurlOption.Postfields, PutData), (CurlOption.Postfieldsize, (long)PutData.Length)],
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary><c>put_buffer</c> with its <c>put_callback</c>: hands out the bytes left, as many as fit each call.</summary>
    /// <param name="data">The bytes to hand out.</param>
    public sealed class PutBuffer(byte[] data)
    {
        private int offset;

        /// <summary><c>put_callback</c>: copies as many of the bytes left as fit into <paramref name="buffer"/>.</summary>
        /// <param name="buffer">Where the bytes go.</param>
        /// <returns>The bytes copied, 0 once all are gone.</returns>
        public long Read(Span<byte> buffer)
        {
            int count = Math.Min(buffer.Length, data.Length - offset);
            data.AsSpan(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }
    }
}
