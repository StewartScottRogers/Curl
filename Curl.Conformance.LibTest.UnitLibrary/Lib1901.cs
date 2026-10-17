using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1901.c</c>: a verbose HTTP/1.1 <c>CURLOPT_POST</c> of the case's URL whose body comes from a read callback
/// giving <see cref="Chunks"/> one a call, with <c>Expect:</c> and <c>Transfer-Encoding: chunked</c> headers and a
/// deliberately wrong <c>CURLOPT_POSTFIELDSIZE</c> of 4, which libcurl must ignore for the chunked body. The perform's
/// code is the program's; an easy handle that cannot be created ends with <c>CURLE_OK</c>, a failing <c>easy_setopt</c>
/// with its code. Libcurl's <c>curl_slist_append</c> cannot fail, so its two checks are left out.
/// </summary>
public sealed class Lib1901 : ILibTest
{
    /// <summary>The chunks the read callback gives, in order.</summary>
    public static IReadOnlyList<string> Chunks { get; } = ["one", "two", "three", "four"];

    /// <inheritdoc/>
    public string ProgramName => "lib1901";

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
        ArgumentNullException.ThrowIfNull(library);
        CurlEasy? easy = library.IsCleanedUp ? null : library.EasyInit();
        if (easy is null)
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Postfieldsize, 4L),
                    (CurlOption.Postfields, null),
                    (CurlOption.Readfunction, (ReadCallback)Reader().Invoke),
                    (CurlOption.Post, 1L),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.HttpVersion, (long)HttpVersion.Http11),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Readdata, null),
                    (CurlOption.Httpheader, CurlSlist.Append(CurlSlist.Append(null, "Expect:"), "Transfer-Encoding: chunked")),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary><c>t1901_read_cb</c>: a read callback giving the next of <see cref="Chunks"/> each call, then 0.</summary>
    /// <returns>A new callback, starting at the first chunk.</returns>
    public static Func<Span<byte>, long> Reader()
    {
        int index = 0;
        return buffer =>
        {
            if (index == Chunks.Count)
            {
                return 0;
            }

            // curlx_strcopy copies only when the chunk and its NUL fit, and the callback answers the length either way.
            string chunk = Chunks[index++];
            if (chunk.Length < buffer.Length)
            {
                _ = Encoding.ASCII.GetBytes(chunk, buffer);
            }

            return chunk.Length;
        };
    }
}
