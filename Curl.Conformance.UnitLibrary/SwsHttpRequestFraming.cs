using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Where one HTTP request ends in the bytes a client has sent, decided the way
/// <c>sws_ProcessRequest</c> in upstream's <c>tests/server/sws.c</c> at <c>curl-8_21_0</c>
/// decides it: the headers end at the first empty line, then a body follows by
/// <c>Transfer-Encoding: chunked</c> (which wins) or by the first non-zero
/// <c>Content-Length</c>, as the <c>&lt;servercmd&gt;</c> commands <c>auth_required</c>,
/// <c>no-expect</c>, <c>skip: N</c> and <c>upgrade</c> change it.
/// </summary>
/// <remarks>
/// A request that ends before bytes the client has already sent leaves them to start the next
/// request, as bytes sws has not yet read from the socket would.
/// </remarks>
internal static class SwsHttpRequestFraming
{
    private const string ContentLengthPrefix = "Content-Length:";

    private const string ChunkedPrefix = "Transfer-Encoding: chunked";

    private const string ExpectContinuePrefix = "Expect: 100-continue";

    /// <summary>The length of the first complete request in <paramref name="received"/>, or -1 while it is incomplete.</summary>
    /// <param name="received">The bytes received and not yet served.</param>
    /// <param name="serverCommands">The test case's <c>&lt;servercmd&gt;</c> commands.</param>
    /// <param name="upgradesConnection">
    /// Set to whether the request asks for an upgrade that <c>upgrade</c> allows, so it ends at its
    /// headers and every byte after them is traffic on the upgraded connection.
    /// </param>
    /// <returns>The request's length in bytes, or -1.</returns>
    public static int FindRequestLength(ReadOnlySpan<byte> received, SwsServerCommands serverCommands, out bool upgradesConnection)
    {
        upgradesConnection = false;
        int headersEnd = received.IndexOf("\r\n\r\n"u8);
        if (headersEnd < 0)
        {
            return -1;
        }

        string[] headerLines = Encoding.Latin1.GetString(received[..headersEnd]).Split("\r\n");
        if (headerLines.Any(line => IsHeader(line, ChunkedPrefix)))
        {
            return FindChunkedEnd(received, headersEnd);
        }

        upgradesConnection = UpgradesConnection(received, serverCommands);
        return upgradesConnection ? headersEnd + 4 : FindBodyEnd(received, headersEnd + 4, headerLines, serverCommands);
    }

    /// <summary>
    /// Whether sws reads a complete request's headers through to its <c>Authorization:</c>
    /// rules: it returns before them at a <c>Transfer-Encoding: chunked</c> header, and at a
    /// <c>Content-Length</c> it cannot read while it has no length yet.
    /// </summary>
    /// <param name="request">The request's bytes, headers and body.</param>
    /// <param name="serverCommands">The test case's <c>&lt;servercmd&gt;</c> commands.</param>
    /// <returns>Whether the authorization rules apply to the request.</returns>
    public static bool ReachesAuthorizationRules(ReadOnlySpan<byte> request, SwsServerCommands serverCommands)
    {
        long bodyLength = 0;
        foreach (string line in Encoding.Latin1.GetString(request[..request.IndexOf("\r\n\r\n"u8)]).Split("\r\n"))
        {
            if (IsHeader(line, ChunkedPrefix) || (bodyLength == 0 && IsHeader(line, ContentLengthPrefix) && ParseContentLength(line[ContentLengthPrefix.Length..]) < 0))
            {
                return false;
            }

            bodyLength = BodyLengthAfter(line, bodyLength, serverCommands);
        }

        return true;
    }

    // sws's req->cl after one header line: the first readable Content-Length less skip: N, and
    // zero again after Expect: 100-continue under no-expect.
    private static long BodyLengthAfter(string line, long bodyLength, SwsServerCommands serverCommands)
    {
        if (bodyLength == 0 && IsHeader(line, ContentLengthPrefix))
        {
            return ContentLengthLessSkipped(line, serverCommands.SkippedBodyBytes);
        }

        return serverCommands.IgnoresExpectedBody && IsHeader(line, ExpectContinuePrefix) ? 0 : bodyLength;
    }

    // auth_required is checked first and ends the request without an upgrade; then "Upgrade:"
    // anywhere in what has been received, matched case-sensitively as sws's strstr does.
    private static bool UpgradesConnection(ReadOnlySpan<byte> received, SwsServerCommands serverCommands) =>
        serverCommands.AllowsUpgrade && !LacksRequiredAuthorization(received, serverCommands) && received.IndexOf("Upgrade:"u8) >= 0;

    private static bool LacksRequiredAuthorization(ReadOnlySpan<byte> received, SwsServerCommands serverCommands) =>
        serverCommands.RequiresAuthorization && received.IndexOf("Authorization:"u8) < 0;

    // auth_required ends a request with no "Authorization:" anywhere in it at its headers.
    private static int FindBodyEnd(ReadOnlySpan<byte> received, int bodyStart, string[] headerLines, SwsServerCommands serverCommands)
    {
        if (LacksRequiredAuthorization(received, serverCommands))
        {
            return bodyStart;
        }

        // A negative length is sws's size_t wrapping round: the body never completes.
        long bodyLength = BodyLength(headerLines, serverCommands);
        return bodyLength >= 0 && received.Length - bodyStart >= bodyLength ? (int)(bodyStart + bodyLength) : -1;
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

    // With no-expect, an Expect: 100-continue header zeroes the length whichever side of
    // Content-Length it is on: sws zeroes the length it has read and ignores any it reads later.
    private static long BodyLength(string[] headerLines, SwsServerCommands serverCommands) =>
        serverCommands.IgnoresExpectedBody && headerLines.Any(line => IsHeader(line, ExpectContinuePrefix))
            ? 0
            : ContentLength(headerLines, serverCommands.SkippedBodyBytes);

    // The first Content-Length that leaves a non-zero length once skip: N is taken off it.
    private static long ContentLength(string[] headerLines, int skippedBodyBytes)
    {
        long bodyLength = 0;
        foreach (string line in headerLines)
        {
            if (bodyLength == 0 && IsHeader(line, ContentLengthPrefix))
            {
                bodyLength = ContentLengthLessSkipped(line, skippedBodyBytes);
            }
        }

        return bodyLength;
    }

    private static bool IsHeader(string line, string prefix) => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static long ContentLengthLessSkipped(string line, int skippedBodyBytes)
    {
        long contentLength = ParseContentLength(line[ContentLengthPrefix.Length..]);
        return contentLength < 0 ? 0 : contentLength - skippedBodyBytes;
    }

    // curlx_str_numblanks: blanks, then digits. A value it rejects is -1, and leaves the length
    // at zero, as sws does when it gives up on the header.
    private static long ParseContentLength(string value)
    {
        string digits = new(value.TrimStart(' ', '\t').TakeWhile(char.IsAsciiDigit).ToArray());
        return long.TryParse(digits, out long contentLength) ? contentLength : -1;
    }
}
