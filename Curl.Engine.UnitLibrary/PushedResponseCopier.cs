namespace Curl.Engine;

/// <summary>
/// Copies an HTTP/2 server push's response (<c>Curl.Original.Protocol.Http</c>'s <c>IHttpPushedResponse.Content</c>: the
/// status line, the header lines, the empty line, then the body) to a pushed transfer's outputs, as curl 8.22.0
/// hands a pushed stream's head to the duplicated handle's header callback and its DATA to its write callback
/// (BL-2469, ADR-0605).
/// </summary>
internal static class PushedResponseCopier
{
    /// <summary>The size of each read from the pushed response.</summary>
    internal const int ReadSize = 16384;

    private static ReadOnlySpan<byte> HeadEnd => "\r\n\r\n"u8;

    /// <summary>
    /// Copies <paramref name="content" /> until the server ends the stream: each header line, with its CRLF, to
    /// <paramref name="headerOutput" /> and every byte after the empty line to <paramref name="bodyOutput" />.
    /// </summary>
    /// <param name="content">The pushed response.</param>
    /// <param name="headerOutput">Where the head goes, line by line.</param>
    /// <param name="bodyOutput">Where the body goes.</param>
    /// <param name="cancellationToken">Stops the copy.</param>
    /// <returns>A task that completes once the response has ended.</returns>
    internal static async Task CopyAsync(Stream content, Stream headerOutput, Stream bodyOutput, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[ReadSize];
        List<byte> head = [];
        bool inHead = true;
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            int bodyStart = inHead ? TakeHead(head, buffer.AsSpan(0, read)) : 0;
            if (bodyStart < 0)
            {
                continue;
            }

            if (inHead)
            {
                inHead = false;
                await WriteHeadLinesAsync([.. head], headerOutput, cancellationToken).ConfigureAwait(false);
            }

            await bodyOutput.WriteAsync(buffer.AsMemory(bodyStart, read - bodyStart), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds <paramref name="chunk" />'s bytes to <paramref name="head" /> up to and including the empty line that
    /// ends it.
    /// </summary>
    /// <returns>Where the body starts in <paramref name="chunk" />, or -1 while the head goes on.</returns>
    private static int TakeHead(List<byte> head, ReadOnlySpan<byte> chunk)
    {
        int alreadyHeld = head.Count;
        head.AddRange(chunk);
        int end = CollectionsMarshalSpan(head).IndexOf(HeadEnd);
        if (end < 0)
        {
            return -1;
        }

        int headLength = end + HeadEnd.Length;
        head.RemoveRange(headLength, head.Count - headLength);
        return headLength - alreadyHeld;
    }

    private static ReadOnlySpan<byte> CollectionsMarshalSpan(List<byte> bytes) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(bytes);

    /// <summary>Writes the head one line at a time, each with its CRLF, as libcurl calls the header callback once per line.</summary>
    private static async Task WriteHeadLinesAsync(byte[] head, Stream headerOutput, CancellationToken cancellationToken)
    {
        int start = 0;
        while (start < head.Length)
        {
            int lineLength = head.AsSpan(start).IndexOf("\r\n"u8) + 2;
            await headerOutput.WriteAsync(head.AsMemory(start, lineLength), cancellationToken).ConfigureAwait(false);
            start += lineLength;
        }
    }
}
