using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Chooses the reply to one request the way upstream's <c>tests/server/sws.c</c> at
/// <c>curl-8_21_0</c> does (<c>sws_ProcessRequest</c> and <c>sws_send_doc</c>): the part
/// number is the number in the request path's last segment modulo 10000 when that number
/// is over 10000, and 0 otherwise; part 0 is <c>&lt;reply&gt;&lt;data&gt;</c> and part N is
/// <c>&lt;dataN&gt;</c>. The part is read as sws's <c>getpart</c> reads it: base64-decoded when
/// it has a <c>base64</c> attribute, with its last byte cut when it has <c>nonewline</c>.
/// </summary>
/// <remarks>
/// The connection closes after a reply that contains <c>swsclose</c>, after an empty or
/// missing part, and after every reply when <c>&lt;servercmd&gt;</c> says <c>swsclose</c>. A
/// request whose first line is not <c>METHOD path HTTP/x.y</c> gets sws's 404 document and a
/// close. A path with no number selects part 0 of this case, as sws does when the runner's
/// command file names the test.
/// </remarks>
internal sealed class SwsHttpReplySelector(UpstreamTestCase testCase, bool closesAfterEveryReply)
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

    /// <summary>The reply to one complete request.</summary>
    /// <param name="request">The request's bytes, headers and body.</param>
    /// <returns>The reply and whether the connection closes after it.</returns>
    public SwsHttpReply Select(ReadOnlySpan<byte> request)
    {
        if (SwsHttpRequestLine.FindPath(request) is not { } path)
        {
            return new SwsHttpReply(Encoding.Latin1.GetBytes(NotFoundDocument), true);
        }

        int partNumber = SwsHttpRequestLine.PartNumber(path);
        byte[] bytes = ReadPart(partNumber == 0 ? "data" : $"data{partNumber}");
        bool closesConnection = closesAfterEveryReply || bytes.Length == 0 || bytes.AsSpan().IndexOf("swsclose"u8) >= 0;
        return new SwsHttpReply(bytes, closesConnection);
    }

    private byte[] ReadPart(string name)
    {
        if (testCase.Find("reply", name) is not { } part)
        {
            return [];
        }

        byte[] bytes = part.Attributes.ContainsKey("base64") ? DecodeBase64(part.Content.Span) : part.Content.ToArray();
        return part.Attributes.ContainsKey("nonewline") && bytes.Length > 0 ? bytes[..^1] : bytes;
    }

    // sws sends nothing when a part does not decode.
    private static byte[] DecodeBase64(ReadOnlySpan<byte> content)
    {
        try
        {
            return Convert.FromBase64String(Encoding.Latin1.GetString(content));
        }
        catch (FormatException)
        {
            return [];
        }
    }
}
