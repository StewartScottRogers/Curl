using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Options.Http;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.LibTest;

/// <summary>
/// <c>lib1576.c</c>, run by cases 1576 to 1580: a verbose upload with headers of <see cref="Body"/> (its size set by
/// <c>CURLOPT_INFILESIZE</c>) with the custom method <c>CURL</c>, a seek callback that rewinds only from the start,
/// <c>Expect:</c> removed, and redirects followed with <c>CURLFOLLOW_FIRSTONLY</c> for cases 1578 and 1580 and
/// <c>CURLFOLLOW_OBEYCODE</c> for the rest, answering the perform's code.
/// </summary>
public sealed class Lib1576 : ILibTest
{
    private static readonly HashSet<int> FirstOnlyCases = [1578, 1580];

    /// <summary>The bytes the read callback hands over: <c>t1576_data</c>.</summary>
    public static ReadOnlyMemory<byte> Body { get; } = Encoding.ASCII.GetBytes("request indicates that the client, which made");

    /// <inheritdoc/>
    public string ProgramName => "lib1576";

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
            // Upstream sets CURLOPT_HTTPHEADER unchecked; Libcurl cannot fail it, so it goes with the checked options.
            _ = await steps.SetOptionsAndPerformAsync(easy, [
                (CurlOption.Writedata, context.StandardOutput),
                (CurlOption.Header, 1L),
                (CurlOption.Verbose, 1L),
                (CurlOption.Url, context.Url),
                (CurlOption.Upload, 1L),
                (CurlOption.Readfunction, (ReadCallback)Read),
                (CurlOption.Seekfunction, (SeekCallback)Seek),
                (CurlOption.Infilesize, (long)Body.Length),
                (CurlOption.Customrequest, "CURL"),
                (CurlOption.Followlocation, FollowFor(context.TestNumber)),
                (CurlOption.Httpheader, CurlSlist.Append(null, "Expect:")),
            ], cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The <c>CURLOPT_FOLLOWLOCATION</c> value for a case: <c>CURLFOLLOW_FIRSTONLY</c> for 1578 and 1580, otherwise <c>CURLFOLLOW_OBEYCODE</c>.</summary>
    /// <param name="testNumber">The case's number.</param>
    /// <returns>The value.</returns>
    public static long FollowFor(int testNumber) => (long)(FirstOnlyCases.Contains(testNumber) ? FollowLocation.FirstOnly : FollowLocation.ObeyCode);

    /// <summary>
    /// <c>t1576_read_cb</c>: <see cref="Body"/> copied into <paramref name="buffer"/> on every call; a buffer too small
    /// for it is left as it is, and the callback still answers the body's length.
    /// </summary>
    /// <param name="buffer">The buffer to fill.</param>
    /// <returns>The body's length.</returns>
    public static long Read(Span<byte> buffer)
    {
        if (buffer.Length >= Body.Length)
        {
            Body.Span.CopyTo(buffer);
        }

        return Body.Length;
    }

    /// <summary><c>t1576_seek_callback</c>: any offset from the start works; any other origin fails.</summary>
    /// <param name="offset">The offset, ignored.</param>
    /// <param name="origin">Where the offset counts from.</param>
    /// <returns><see cref="SeekResult.Ok"/> from the start, otherwise <see cref="SeekResult.Fail"/>.</returns>
    public static SeekResult Seek(long offset, SeekOrigin origin) => origin == SeekOrigin.Begin ? SeekResult.Ok : SeekResult.Fail;
}
