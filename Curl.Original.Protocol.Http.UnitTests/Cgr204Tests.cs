using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http.UnitTests;

[TestClass]
public class Cgr204Tests
{
    [TestMethod]
    public void EndHead_HeaderCrossesTheLimit_HeadBytesHoldThatHeaderOnce()
    {
        // Real curl 8.22.0 writes the header that crossed the limit once and counts it once (CGR-204).
        HttpResponseHeadBuilder builder = new();
        builder.StartHead(Line("HTTP/1.1 200 OK"));
        for (int index = 0; index < 307; index++)
        {
            builder.AddLine(Line(HeaderOfLength(1000)));
        }

        builder.AddLine(Line(HeaderOfLength(300)));

        HttpTransferException failure = Assert.ThrowsExactly<HttpTransferException>(() => builder.EndHead(Line(string.Empty)));

        Assert.AreEqual(CurlExitCode.RecvError, failure.ExitCode);
        Assert.AreEqual(17 + (307 * 1000) + 300, builder.HeadBytesWithPendingHeader().Length);
        Assert.AreEqual(308, builder.HeaderCountWithPending);
    }

    private static HttpLine Line(string content) => HttpLine.Split(Encoding.Latin1.GetBytes(content + "\r\n"));

    /// <summary>A header line of <paramref name="length" /> bytes, its CRLF included.</summary>
    private static string HeaderOfLength(int length) => "X-A: " + new string('a', length - 7);
}
