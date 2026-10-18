using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib553.c</c>: a verbose POST with headers on of 40960 <c>A</c>s from a read callback, at most 1024 a call,
/// with eight custom headers <c>HeaderN: </c> of 5000 <c>A</c>s each and an empty <c>Expect:</c>.
/// </summary>
public sealed class Lib553 : ILibTest
{
    /// <summary>How many bytes the program posts.</summary>
    public const int PostLength = 40960;

    /// <inheritdoc/>
    public string ProgramName => "lib553";

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
            CurlSlist? headers = null;
            for (int i = 0; i < 8; i++)
            {
                headers = CurlSlist.Append(headers, $"Header{i}: {new string('A', 5000)}");
            }

            headers = CurlSlist.Append(headers, "Expect: ");
            int remaining = PostLength;
            await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Httpheader, headers),
                    (CurlOption.Post, 1L),
                    (CurlOption.Postfieldsize, (long)PostLength),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                    (CurlOption.Readfunction, (ReadCallback)(buffer => ReadAs(buffer, ref remaining))),
                ],
                cancellationToken).ConfigureAwait(false);
            CurlSlist.FreeAll(headers);
        }
    }

    /// <summary><c>myreadfunc</c>: up to 1024 <c>A</c>s, never more than <paramref name="remaining"/>.</summary>
    /// <param name="buffer">Where the bytes go.</param>
    /// <param name="remaining">The bytes still to post, lowered by those given.</param>
    /// <returns>How many bytes were given.</returns>
    public static long ReadAs(Span<byte> buffer, ref int remaining)
    {
        int size = Math.Min(Math.Min(buffer.Length, remaining), 1024);
        buffer[..size].Fill((byte)'A');
        remaining -= size;
        return size;
    }
}
