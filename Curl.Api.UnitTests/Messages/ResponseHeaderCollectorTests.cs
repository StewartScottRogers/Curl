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
