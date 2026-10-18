using Curl.Api.Callbacks;
using Curl.Api.Options.Authentication;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib579.c</c>: a chunked POST of <c>one</c>, <c>two</c>, <c>three</c> and <c>and a final longer crap: four</c>
/// from a read callback, with Digest authentication as <c>foo:bar</c>, headers on and verbose. Its progress
/// callback appends <c>Progress: start UL %zu/%zu</c> to the file in <c>libtest_arg2</c> on its first call and
/// <c>Progress: end UL %zu/%zu</c> when the upload count falls back to zero after a non-zero one; the program
/// appends the end line once more after the perform. A file that does not open sends the line to stderr with a
/// <c>cannot open</c> line after it.
/// </summary>
public sealed class Lib579 : ILibTest
{
    private static readonly byte[][] Posts = [.. new[] { "one", "two", "three", "and a final longer crap: four" }.Select(System.Text.Encoding.ASCII.GetBytes)];

    /// <inheritdoc/>
    public string ProgramName => "lib579";

    /// <inheritdoc/>
    public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
        LibTestRun.WithLibraryAsync(context, (steps, library) => TestAsync(context, steps, library, cancellationToken));

    /// <summary>The body after <c>curl_global_init</c>: <c>curl_easy_init</c>, the options, one perform and the final report.</summary>
    /// <param name="context">The program's context.</param>
    /// <param name="steps">The program's steps, carrying its <c>result</c>.</param>
    /// <param name="library">The initialized library.</param>
    /// <param name="cancellationToken">Stops the transfer.</param>
    /// <returns>A task that completes at the program's cleanup.</returns>
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
            var progress = new Lib579Progress(context.Argument2, context.StandardError);
            int counter = 0;
            _ = steps.EasySetOptions(
                easy,
                [
                    (CurlOption.Writedata, context.StandardOutput),
                    (CurlOption.Url, context.Url),
                    (CurlOption.Post, 1L),
                    (CurlOption.Readfunction, (ReadCallback)(buffer => Read(buffer, ref counter))),
                    (CurlOption.Verbose, 1L),
                    (CurlOption.Header, 1L),
                    (CurlOption.Httpheader, CurlSlist.Append(null, "Transfer-Encoding: chunked")),
                    (CurlOption.Httpauth, (long)HttpAuthenticationMethods.Digest),
                    (CurlOption.Userpwd, "foo:bar"),
                    (CurlOption.Noprogress, 0L),
                    (CurlOption.Progressfunction, (ProgressCallback)progress.Callback),
                ]);
            _ = await steps.EasyPerformAsync(easy, cancellationToken).ConfigureAwait(false);
            progress.FinalReport();
        }
    }

    /// <summary><c>t579_read_cb</c>: the next of the four posts, whole, then 0; 0 too for an empty buffer.</summary>
    /// <param name="buffer">Where the data goes.</param>
    /// <param name="counter">How many posts went already.</param>
    /// <returns>The bytes written.</returns>
    public static long Read(Span<byte> buffer, ref int counter)
    {
        if (buffer.IsEmpty || counter >= Posts.Length)
        {
            return 0;
        }

        byte[] data = Posts[counter++];
        data.CopyTo(buffer);
        return data.Length;
    }
}
