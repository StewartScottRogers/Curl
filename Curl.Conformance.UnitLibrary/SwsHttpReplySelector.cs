using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Chooses the reply to one request the way upstream's <c>tests/server/sws.c</c> at
/// <c>curl-8_21_0</c> does (<c>sws_ProcessRequest</c>, <c>service_connection</c> and
/// <c>sws_send_doc</c>): the part number is the number in the request path's last segment
/// modulo 10000 when that number is over 10000, and 0 otherwise, then changed by the
/// request's <c>Authorization:</c> header and by a previous reply's <c>swsbounce</c>; part 0 is
/// <c>&lt;reply&gt;&lt;data&gt;</c> and part N is <c>&lt;dataN&gt;</c>, or <c>&lt;connect&gt;</c> and
/// <c>&lt;connectN&gt;</c> for a <c>CONNECT host:port</c> request. The part is read as sws's
/// <c>getpart</c> reads it after <c>runtests.pl</c>'s <c>prepro</c> has forced the line endings
/// its <c>crlf</c> attribute asks for: base64-decoded when it has a <c>base64</c> attribute, with its last
/// byte cut when it has <c>nonewline</c>.
/// </summary>
/// <remarks>
/// <para>
/// The first of these that the request contains, anywhere in its bytes, changes the part
/// number, unless sws returned before looking (a <c>Transfer-Encoding: chunked</c> request, or
/// one with a <c>Content-Length</c> it cannot read): <c>Authorization: Negotiate</c> counts up by one from the part number of the first
/// such request; <c>Authorization: Digest</c> adds 1000; <c>Authorization: NTLM TlRMTVNTUAAD</c>
/// (type 3) adds 1002; <c>Authorization: NTLM TlRMTVNTUAAB</c> (type 1) adds 1001; and
/// <c>Authorization: Basic</c> adds 1 when the part number is already 1000 or more. A
/// <c>Proxy-Authorization:</c> header contains the same text, so it counts too. After a reply
/// that contains <c>swsbounce</c>, the next request gets that reply's part number plus one,
/// whatever it asked for. The Negotiate count and the bounce are kept across connections, as
/// sws keeps them in statics; sws resets both when the test number changes, which here it never
/// does, because the emulation serves one case and takes every request to name it.
/// </para>
/// <para>
/// The connection closes after a reply that contains <c>swsclose</c>, after an empty or
/// missing part, and after every reply when <c>&lt;servercmd&gt;</c> says <c>swsclose</c>. A
/// request whose first line is not <c>METHOD path HTTP/x.y</c> gets sws's 404 document and a
/// close, and ends any bounce. A path with no number selects part 0 of this case, as sws does
/// when the runner's command file names the test.
/// </para>
/// </remarks>
internal sealed class SwsHttpReplySelector(UpstreamTestCase testCase, SwsServerCommands serverCommands)
{
    private const string NotFoundDocument =
        "HTTP/1.1 404 Not Found\r\n"
        + "Server: sws/1.0\r\n"
        + "Connection: close\r\n"
        + "Content-Type: text/html\r\n\r\n"
        + "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">\n"
        + "<HTML><HEAD>\n"
        + "<TITLE>404 Not Found</TITLE>\n"
        + "</HEAD><BODY>\n"
        + "<H1>Not Found</H1>\n"
        + "The requested URL was not found on this server.\n"
        + "<P><HR><ADDRESS>sws/1.0</ADDRESS>\n"
        + "</BODY></HTML>\n";

    // The headers that add to the part number, in the order sws tests them after Negotiate.
    private static readonly (byte[] Text, int Added)[] AddingAuthorizations =
    [
        ("Authorization: Digest"u8.ToArray(), 1000),
        ("Authorization: NTLM TlRMTVNTUAAD"u8.ToArray(), 1002),
        ("Authorization: NTLM TlRMTVNTUAAB"u8.ToArray(), 1001),
    ];

    // sws's prev_partno in the Negotiate branch: null until the first Negotiate request.
    private int? lastNegotiatePartNumber;

    // The part number of the last reply when it contained swsbounce; null otherwise.
    private int? bouncedPartNumber;

    /// <summary>The reply to one complete request.</summary>
    /// <param name="request">The request's bytes, headers and body.</param>
    /// <returns>The reply and whether the connection closes after it.</returns>
    public SwsHttpReply Select(ReadOnlySpan<byte> request)
    {
        if (SwsHttpRequestLine.FindPath(request) is not { } path)
        {
            bouncedPartNumber = null;
            return new SwsHttpReply(Encoding.Latin1.GetBytes(NotFoundDocument), true, false);
        }

        int partNumber = BouncedOrAuthorizedPartNumber(request, path);
        byte[] bytes = ReadPart(PartName(request, path, partNumber));
        RememberBounce(bytes, partNumber);
        bool closesConnection = serverCommands.ClosesAfterEveryReply || bytes.Length == 0 || bytes.AsSpan().IndexOf("swsclose"u8) >= 0;
        return new SwsHttpReply(bytes, closesConnection, true);
    }

    private static string PartName(ReadOnlySpan<byte> request, string path, int partNumber)
    {
        string section = SwsHttpRequestLine.IsConnect(request, path) ? "connect" : "data";
        return partNumber == 0 ? section : $"{section}{partNumber}";
    }

    // The authorization rules run for every request, so the Negotiate count moves even when a
    // bounce then decides the part, as in sws.
    private int BouncedOrAuthorizedPartNumber(ReadOnlySpan<byte> request, string path)
    {
        int pathPartNumber = SwsHttpRequestLine.PartNumber(path);
        int authorizedPartNumber = SwsHttpRequestFraming.ReachesAuthorizationRules(request, serverCommands)
            ? AuthorizedPartNumber(request, pathPartNumber)
            : pathPartNumber;
        return bouncedPartNumber is { } bounced ? bounced + 1 : authorizedPartNumber;
    }

    // The first rule that matches applies, in sws's order.
    private int AuthorizedPartNumber(ReadOnlySpan<byte> request, int partNumber)
    {
        if (request.IndexOf("Authorization: Negotiate"u8) >= 0)
        {
            lastNegotiatePartNumber = (lastNegotiatePartNumber ?? partNumber) + 1;
            return lastNegotiatePartNumber.Value;
        }

        foreach ((byte[] text, int added) in AddingAuthorizations)
        {
            if (request.IndexOf(text) >= 0)
            {
                return partNumber + added;
            }
        }

        return partNumber >= 1000 && request.IndexOf("Authorization: Basic"u8) >= 0 ? partNumber + 1 : partNumber;
    }

    private void RememberBounce(byte[] bytes, int partNumber) =>
        bouncedPartNumber = bytes.AsSpan().IndexOf("swsbounce"u8) >= 0 ? partNumber : null;

    private byte[] ReadPart(string name)
    {
        if (testCase.Find("reply", name) is not { } part)
        {
            return [];
        }

        byte[] bytes = UpstreamTestPartBodies.Decoded(part);
        return part.Attributes.ContainsKey("nonewline") && bytes.Length > 0 ? bytes[..^1] : bytes;
    }
}
