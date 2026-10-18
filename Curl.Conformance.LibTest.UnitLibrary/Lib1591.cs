using System.Text;
using Curl.Api.Callbacks;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1591.c</c>: a chunked upload of <see cref="Body"/> to the case's URL with a <c>Trailer:</c> header naming the two
/// trailers <see cref="AddTrailers"/> sends after it. The perform's code is the program's.
/// </summary>
public sealed class Lib1591 : ILibTest
{
    /// <summary>The <c>Trailer:</c> header lib1591 and lib1598 send.</summary>
    public const string TrailerHeader = "Trailer: my-super-awesome-trailer, my-other-awesome-trailer";

    /// <summary>The uploaded body, <c>testdata</c>.</summary>
    public static ReadOnlyMemory<byte> Body { get; } = Encoding.ASCII.GetBytes("Hello Cloud!\r\n");

    /// <inheritdoc/>
    public string ProgramName => "lib1591";

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
        if (!steps.EasyInitOrMajorBad(library, out CurlEasy? easy))
        {
            return;
        }

        await using (easy.ConfigureAwait(false))
        {
            _ = await steps.SetOptionsAndPerformAsync(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Httpheader, CurlSlist.Append(null, TrailerHeader)),
                    (CurlOption.Upload, 1L),
                    (CurlOption.Readfunction, Reader()),
                    (CurlOption.Trailerfunction, new TrailerCallback(AddTrailers)),
                    (CurlOption.Trailerdata, null),
                ],
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// <c>t1591_read_cb</c>, one per transfer: as much of <see cref="Body"/> as is left and fits, copied, as upstream
    /// copies it, from the body's start; nothing once all of it was read.
    /// </summary>
    /// <returns>The read callback, its <c>consumed</c> count at zero.</returns>
    public static ReadCallback Reader()
    {
        int consumed = 0;
        return buffer =>
        {
            int amount = Math.Min(buffer.Length, Body.Length - consumed);
            consumed += amount;
            Body.Span[..amount].CopyTo(buffer);
            return amount;
        };
    }

    /// <summary>
    /// <c>t1591_trailers_callback</c>, which <c>lib1598.c</c> repeats: adds <c>my-super-awesome-trailer: trail1</c> and
    /// <c>my-other-awesome-trailer: trail2</c>. Upstream's abort on a failed <c>curl_slist_append</c> is left out, as a
    /// managed append cannot fail.
    /// </summary>
    /// <param name="trailers">The trailer lines to send.</param>
    /// <returns><see cref="TrailerResult.Ok"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="trailers"/> is <see langword="null"/>.</exception>
    public static TrailerResult AddTrailers(IList<string> trailers)
    {
        ArgumentNullException.ThrowIfNull(trailers);
        trailers.Add("my-super-awesome-trailer: trail1");
        trailers.Add("my-other-awesome-trailer: trail2");
        return TrailerResult.Ok;
    }
}
