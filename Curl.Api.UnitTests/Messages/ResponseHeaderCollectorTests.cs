using System.Text;

namespace Curl.Api.Messages;

[TestClass]
public sealed class ResponseHeaderCollectorTests
{
    private static ResponseHeaderCollector Collect(params string[] lines)
    {
        ResponseHeaderCollector collector = new();
        foreach (string line in lines)
        {
            byte[] bytes = Encoding.Latin1.GetBytes(line);
            Assert.AreEqual(bytes.Length, collector.Take(bytes));
        }

        return collector;
    }

    [TestMethod]
    public void Take_KeepsCasingOrderAndDuplicates()
    {
        ResponseHeaderCollector collector = Collect(
            "HTTP/1.1 200 OK\r\n", "X-Thing:  a \r\n", "set-cookie: one\r\n", "Set-Cookie:two\r\n", "\r\n");

        Assert.AreEqual("OK", collector.ReasonPhrase);
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("X-Thing", "a"), new("set-cookie", "one"), new("Set-Cookie", "two") },
            collector.Headers.ToArray());
        Assert.AreEqual(0, collector.Trailers.Count);
    }

    [TestMethod]
    [DataRow("HTTP/1.0 301 Moved\r\n", 301, 1L)]
    [DataRow("HTTP/1.1 200 OK\r\n", 200, 2L)]
    [DataRow("HTTP/2 204\r\n", 204, 3L)]
    [DataRow("HTTP/3 404\r\n", 404, 30L)]
    [DataRow("HTTP/0.9 2xx odd\r\n", 0, 0L)]
    [DataRow("HTTP/1.1\r\n", 0, 2L)]
    public void Take_StatusLine_GivesStatusCodeAndCurlHttpVersion(string statusLine, int statusCode, long httpVersion)
    {
        ResponseHeaderCollector collector = Collect(statusLine);

        Assert.AreEqual(statusCode, collector.StatusCode);
        Assert.AreEqual(httpVersion, collector.HttpVersion);
    }

    [TestMethod]
    public void Take_NoStatusLine_LeavesStatusCodeAndVersionZero()
    {
        ResponseHeaderCollector collector = Collect("X-A: 1\r\n");

        Assert.AreEqual(0, collector.StatusCode);
        Assert.AreEqual(0L, collector.HttpVersion);
    }

    [TestMethod]
    public void Take_StatusLineWithoutReason_GivesEmptyReason()
    {
        ResponseHeaderCollector collector = Collect("HTTP/2 204\r\n", "\r\n");

        Assert.AreEqual("", collector.ReasonPhrase);
    }

    [TestMethod]
    public void Take_ReasonWithSpaces_KeepsItWhole()
    {
        ResponseHeaderCollector collector = Collect("HTTP/1.1 404 Not Found\r\n");

        Assert.AreEqual("Not Found", collector.ReasonPhrase);
    }

    [TestMethod]
    public void Take_NewStatusLine_DropsTheEarlierResponse()
    {
        ResponseHeaderCollector collector = Collect(
            "HTTP/1.1 301 Moved\r\n", "Location: /b\r\n", "\r\n", "Old: trailer\r\n",
            "HTTP/1.1 200 OK\r\n", "Final: yes\r\n", "\r\n");

        Assert.AreEqual("OK", collector.ReasonPhrase);
        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("Final", "yes") }, collector.Headers.ToArray());
        Assert.AreEqual(0, collector.Trailers.Count);
    }

    [TestMethod]
    public void Take_FieldsAfterTheBlankLine_AreTrailers()
    {
        ResponseHeaderCollector collector = Collect(
            "HTTP/1.1 200 OK\r\n", "Transfer-Encoding: chunked\r\n", "\r\n", "X-Checksum: abc\r\n", "\r\n");

        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[] { new("Transfer-Encoding", "chunked") }, collector.Headers.ToArray());
        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("X-Checksum", "abc") }, collector.Trailers.ToArray());
    }

    [TestMethod]
    public void Take_LineWithoutColon_IsANameWithEmptyValue()
    {
        ResponseHeaderCollector collector = Collect("HTTP/1.0 200 OK\n", "Odd\n");

        CollectionAssert.AreEqual(new KeyValuePair<string, string>[] { new("Odd", "") }, collector.Headers.ToArray());
    }

    [TestMethod]
    public void Take_Latin1Bytes_AreKept()
    {
        ResponseHeaderCollector collector = new();

        collector.Take([(byte)'N', (byte)':', (byte)' ', 0xE9, (byte)'\r', (byte)'\n']);

        Assert.AreEqual("é", collector.Headers[0].Value);
    }
}
