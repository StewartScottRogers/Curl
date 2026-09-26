using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Where one HTTP request ends in the bytes a client has sent, decided the way
/// <c>sws_ProcessRequest</c> in upstream's <c>tests/server/sws.c</c> at <c>curl-8_21_0</c>
/// decides it: the headers end at the first empty line, then a body follows by
/// <c>Transfer-Encoding: chunked</c> (which wins) or by the first non-zero
/// <c>Content-Length</c>.
/// </summary>
internal static class SwsHttpRequestFraming
{
    private const string ContentLengthPrefix = "Content-Length:";

    private const string ChunkedPrefix = "Transfer-Encoding: chunked";

    /// <summary>The length of the first complete request in <paramref name="received"/>, or -1 while it is incomplete.</summary>
    /// <param name="received">The bytes received and not yet served.</param>
    /// <returns>The request's length in bytes, or -1.</returns>
    public static int FindRequestLength(ReadOnlySpan<byte> received)
    {
        int headersEnd = received.IndexOf("\r\n\r\n"u8);
        if (headersEnd < 0)
        {
            return -1;
        }

        string[] headerLines = Encoding.Latin1.GetString(received[..headersEnd]).Split("\r\n");
        if (headerLines.Any(line => line.StartsWith(ChunkedPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return FindChunkedEnd(received, headersEnd);
        }

        long bodyEnd = headersEnd + 4 + ContentLength(headerLines);
        return received.Length >= bodyEnd ? (int)bodyEnd : -1;
    }

    // sws looks for "\r\n0\r\n" (the last chunk) from the headers' closing line break on, then
    // for the empty line that ends the trailers after it.
    private static int FindChunkedEnd(ReadOnlySpan<byte> received, int headersEnd)
    {
        int lastChunkStart = headersEnd + 2;
        int lastChunk = received[lastChunkStart..].IndexOf("\r\n0\r\n"u8);
        if (lastChunk < 0)
        {
            return -1;
        }

        int trailersStart = lastChunkStart + lastChunk + 3;
        int trailersEnd = received[trailersStart..].IndexOf("\r\n\r\n"u8);
        return trailersEnd < 0 ? -1 : trailersStart + trailersEnd + 4;
    }

    private static long ContentLength(string[] headerLines)
    {
        long contentLength = 0;
        foreach (string line in headerLines)
        {
            if (contentLength == 0 && line.StartsWith(ContentLengthPrefix, StringComparison.OrdinalIgnoreCase))
            {
                contentLength = ParseContentLength(line[ContentLengthPrefix.Length..]);
            }
        }

        return contentLength;
    }

    // curlx_str_numblanks: blanks, then digits. A value it rejects leaves the length at zero,
    // as sws does when it gives up on the header.
    private static long ParseContentLength(string value)
    {
        string digits = new(value.TrimStart(' ', '\t').TakeWhile(char.IsAsciiDigit).ToArray());
        return long.TryParse(digits, out long contentLength) ? contentLength : 0;
    }
}
