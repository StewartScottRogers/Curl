using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins what <see cref="SwsHttpReplySelector.Select"/> itself says about closing the connection,
/// since the connection sends sws's 404 document with a close whatever the reply says (AF-0428).
/// </summary>
[TestClass]
public sealed class SwsHttpReplySelectorTests
{
    private const string KeepAliveCase = "<testcase>\n<reply>\n<data>\nHTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nhi\n</data>\n</reply>\n</testcase>\n";

    [TestMethod]
    [DataRow("GARBAGE\r\n\r\n")]
    [DataRow("GET /1234\r\n\r\n")]
    [DataRow("GET  HTTP/1.1\r\n\r\n")]
    public void Select_RequestLineWithoutAPath_ClosesTheConnectionAfterTheNotFoundDocument(string request)
    {
        SwsHttpReply reply = Selector().Select(Encoding.Latin1.GetBytes(request));

        StringAssert.StartsWith(Encoding.Latin1.GetString(reply.Bytes), "HTTP/1.1 404 Not Found\r\n");
        Assert.IsTrue(reply.ClosesConnection);
        Assert.IsFalse(reply.IsFromTestCase);
    }

    [TestMethod]
    public void Select_RequestForAPartWithoutSwsclose_KeepsTheConnectionOpen()
    {
        SwsHttpReply reply = Selector().Select("GET /1234 HTTP/1.1\r\n\r\n"u8);

        StringAssert.StartsWith(Encoding.Latin1.GetString(reply.Bytes), "HTTP/1.1 200 OK\r\n");
        Assert.IsFalse(reply.ClosesConnection);
        Assert.IsTrue(reply.IsFromTestCase);
    }

    private static SwsHttpReplySelector Selector() =>
        new(UpstreamTestCaseParser.Parse(Encoding.Latin1.GetBytes(KeepAliveCase)).TestCase!, SwsServerCommands.Read([]));
}
