using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestHeadFormatter" /> to the request heads curl 8.21.0 sent a
/// loopback server through <c>Record-CurlExchange.ps1</c>. Each expected head is the
/// measured bytes, and the command that produced it is in the BL-172 Notes.
/// </summary>
[TestClass]
public sealed class HttpRequestHeadFormatterTests
{
    private const string Url = "http://127.0.0.1:18091/";

    private const string DefaultHeaders = "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n";

    [TestMethod]
    [DataRow("http://127.0.0.1:18091/a?b", "GET /a?b HTTP/1.1\r\n" + DefaultHeaders + "\r\n", DisplayName = "Default GET")]
    [DataRow("http://127.0.0.1:18091", "GET / HTTP/1.1\r\n" + DefaultHeaders + "\r\n", DisplayName = "No path sends /")]
    [DataRow("http://127.0.0.1:18091/p?q#frag", "GET /p?q HTTP/1.1\r\n" + DefaultHeaders + "\r\n", DisplayName = "Fragment dropped")]
    [DataRow("http://EXAMPLE.com/p", "GET /p HTTP/1.1\r\nHost: EXAMPLE.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "Port 80 omitted, case kept")]
    [DataRow("https://example.com/", "GET / HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "Port 443 omitted for https")]
    [DataRow("https://example.com:80/", "GET / HTTP/1.1\r\nHost: example.com:80\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "Port 80 kept for https")]
    [DataRow("http://[::1]:18091/", "GET / HTTP/1.1\r\nHost: [::1]:18091\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "IPv6 bracketed")]
    [DataRow("http://[::1]/", "GET / HTTP/1.1\r\nHost: [::1]\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "IPv6 on port 80")]
    [DataRow("http://[0::1]/", "GET / HTTP/1.1\r\nHost: [::1]\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "IPv6 not as written falls back to Uri.Host")]
    public void Format_NoOptions_SendsCurlsDefaultHead(string url, string expected)
    {
        AssertHead(expected, new Uri(url), null);
    }

    [TestMethod]
    [DataRow("User-Agent: x", "Host: 127.0.0.1:18091\r\nAccept: */*\r\nUser-Agent: x\r\n", DisplayName = "Replace User-Agent moves it last")]
    [DataRow("accept: text/plain", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\naccept: text/plain\r\n", DisplayName = "Replace matches any case")]
    [DataRow("Host: h", "Host: h\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Replace Host keeps its slot")]
    [DataRow("Host:h", "Host:h\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Host spacing kept")]
    [DataRow("Host:   ", "Host:   \r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Blank Host sent")]
    [DataRow("host: h", "Host: h\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Host name capitalised")]
    [DataRow("Host;", "Host:\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Host; sends it empty")]
    [DataRow("Host; y", "Host: y\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Host; y sends y")]
    [DataRow("Host: a\nHost: b\nHOST;", "Host: a\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "First Host wins")]
    [DataRow("X-A: 1\nHost: h\nX-B: 2", "Host: h\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nX-A: 1\r\nX-B: 2\r\n", DisplayName = "Custom Host not repeated")]
    [DataRow("X-B: 2\nAccept: a/b\nX-A: 1", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nX-B: 2\r\nAccept: a/b\r\nX-A: 1\r\n", DisplayName = "Command-line order")]
    [DataRow("X-A: 1\nX-A: 2", DefaultHeaders + "X-A: 1\r\nX-A: 2\r\n", DisplayName = "Duplicate custom")]
    [DataRow("Accept: 1\nAccept: 2", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nAccept: 1\r\nAccept: 2\r\n", DisplayName = "Duplicate replacement")]
    [DataRow("X-A:   1  \nX-B:1", DefaultHeaders + "X-A:   1  \r\nX-B:1\r\n", DisplayName = "Value verbatim")]
    [DataRow("Accept-Language: x\nHostname: y\nUser-Agents: z", DefaultHeaders + "Accept-Language: x\r\nHostname: y\r\nUser-Agents: z\r\n", DisplayName = "Longer names replace nothing")]
    [DataRow("Accept : x", DefaultHeaders + "Accept : x\r\n", DisplayName = "Blank before colon replaces nothing")]
    [DataRow(" Accept: x", DefaultHeaders + " Accept: x\r\n", DisplayName = "Leading blank replaces nothing")]
    [DataRow("X A: 1", DefaultHeaders + "X A: 1\r\n", DisplayName = "Blank inside name sent")]
    public void Format_ReplacingHeader_SendsMeasuredHead(string headers, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", new Uri(Url), new HttpRequestOptions { Headers = headers.Split('\n') });
    }

    [TestMethod]
    [DataRow("Accept:", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\n", DisplayName = "Remove Accept")]
    [DataRow("Accept:   ", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\n", DisplayName = "Remove Accept, blanks after colon")]
    [DataRow("Accept; y", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\n", DisplayName = "Accept; y removes Accept")]
    [DataRow("Host:", "User-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Remove Host")]
    [DataRow("Host:\nHost: x", "User-Agent: curl/8.21.0\r\nAccept: */*\r\nHost: x\r\n", DisplayName = "Removed Host, later Host sent last")]
    [DataRow("X-A:\nX-C:   ", DefaultHeaders, DisplayName = "Removing a header curl does not send")]
    [DataRow("Foo\nX-A: 1", DefaultHeaders + "X-A: 1\r\n", DisplayName = "No colon or semicolon dropped")]
    [DataRow(":x\nX-A: 1", DefaultHeaders + "X-A: 1\r\n", DisplayName = "Empty name before colon dropped")]
    [DataRow(";\nX-A: 1", DefaultHeaders + "X-A: 1\r\n", DisplayName = "Empty name before semicolon dropped")]
    [DataRow("X-B; y", DefaultHeaders, DisplayName = "Value after semicolon dropped")]
    [DataRow("X-B;  ", DefaultHeaders, DisplayName = "Blanks after semicolon dropped")]
    public void Format_RemovingHeader_SendsMeasuredHead(string headers, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", new Uri(Url), new HttpRequestOptions { Headers = headers.Split('\n') });
    }

    [TestMethod]
    [DataRow("X-B;", DefaultHeaders + "X-B:\r\n", DisplayName = "Custom header sent empty")]
    [DataRow("Accept;\nX-A: 1", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nAccept:\r\nX-A: 1\r\n", DisplayName = "Accept sent empty")]
    [DataRow("User-Agent;", "Host: 127.0.0.1:18091\r\nAccept: */*\r\nUser-Agent:\r\n", DisplayName = "User-Agent sent empty")]
    public void Format_EmptyHeader_SendsNameAndColon(string headers, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", new Uri(Url), new HttpRequestOptions { Headers = headers.Split('\n') });
    }

    [TestMethod]
    public void Format_CustomMethodAndRemovedAccept_SendsMeasuredHead()
    {
        HttpRequestOptions options = new() { CustomMethod = "PUT", Headers = ["Accept:", "X-A: 1"] };

        AssertHead("PUT / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nX-A: 1\r\n\r\n", new Uri(Url), options);
    }

    [TestMethod]
    public void Format_CustomMethod_SentVerbatim()
    {
        AssertHead("get / HTTP/1.1\r\n" + DefaultHeaders + "\r\n", new Uri(Url), new HttpRequestOptions { CustomMethod = "get" });
    }

    [TestMethod]
    [DataRow(null, DefaultHeaders, DisplayName = "Null sends curl/8.21.0")]
    [DataRow("agent/1", "Host: 127.0.0.1:18091\r\nUser-Agent: agent/1\r\nAccept: */*\r\n", DisplayName = "-A agent/1")]
    [DataRow("", "Host: 127.0.0.1:18091\r\nAccept: */*\r\n", DisplayName = "Empty omits it")]
    public void Format_UserAgent_SendsMeasuredHead(string? userAgent, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", new Uri(Url), new HttpRequestOptions { UserAgent = userAgent });
    }

    [TestMethod]
    [DataRow("a", DisplayName = "-A a -H 'User-Agent: h'")]
    [DataRow("", DisplayName = "-A '' -H 'User-Agent: h'")]
    public void Format_UserAgentAndCustomUserAgent_SendsCustomLast(string userAgent)
    {
        HttpRequestOptions options = new() { UserAgent = userAgent, Headers = ["User-Agent: h"] };

        AssertHead("GET / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nAccept: */*\r\nUser-Agent: h\r\n\r\n", new Uri(Url), options);
    }

    [TestMethod]
    public void Format_UserAgentAndRemovedUserAgent_SendsNone()
    {
        HttpRequestOptions options = new() { UserAgent = "a", Headers = ["User-Agent:"] };

        AssertHead("GET / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nAccept: */*\r\n\r\n", new Uri(Url), options);
    }

    [TestMethod]
    [DataRow("http://r.example/x", DefaultHeaders + "Referer: http://r.example/x\r\n", DisplayName = "-e sends Referer after Accept")]
    [DataRow("", DefaultHeaders, DisplayName = "Empty sends none")]
    [DataRow(null, DefaultHeaders, DisplayName = "Null sends none")]
    public void Format_Referer_SendsMeasuredHead(string? referer, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", new Uri(Url), new HttpRequestOptions { Referer = referer });
    }

    [TestMethod]
    [DataRow("Referer: h", DefaultHeaders + "Referer: h\r\n", DisplayName = "-H Referer replaces -e")]
    [DataRow("Referer:", DefaultHeaders, DisplayName = "-H Referer: removes -e")]
    public void Format_RefererAndCustomReferer_SendsMeasuredHead(string header, string expectedHeaders)
    {
        HttpRequestOptions options = new() { Referer = "http://r/", Headers = [header] };

        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", new Uri(Url), options);
    }

    [TestMethod]
    public void Format_NonAsciiHeader_SendsLatin1WithBestFit()
    {
        byte[] expected = [.. Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n" + DefaultHeaders + "X-A: "), 0xE9, .. "\r\nX-B: A\r\n\r\n"u8];

        byte[] head = HttpRequestHeadFormatter.Format(new Uri(Url), new HttpRequestOptions { Headers = ["X-A: \u00E9", "X-B: \u0100"] });

        CollectionAssert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_DefaultOptions_MatchesNullOptions()
    {
        CollectionAssert.AreEqual(
            HttpRequestHeadFormatter.Format(new Uri(Url), null),
            HttpRequestHeadFormatter.Format(new Uri(Url), new HttpRequestOptions()));
    }

    private static void AssertHead(string expected, Uri url, HttpRequestOptions? options)
    {
        Assert.AreEqual(expected, Encoding.Latin1.GetString(HttpRequestHeadFormatter.Format(url, options)));
    }
}
