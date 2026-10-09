using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRequestHeadFormatter" /> to the request heads curl 8.21.0 sent a
/// loopback server through <c>Record-CurlExchange.ps1</c>. Each expected head is the
/// measured bytes, and the command that produced it is in the BL-172 Notes.
/// </summary>
[TestClass]
public sealed partial class HttpRequestHeadFormatterTests
{
    private const string Url = "http://127.0.0.1:18091/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
    [DataRow("http://[0::1]/", "GET / HTTP/1.1\r\nHost: [::1]\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "IPv6 as CurlUrl.Host normalises it")]
    public void Format_NoOptions_SendsCurlsDefaultHead(string url, string expected)
    {
        AssertHead(expected, CurlUrl.Parse(url), null);
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
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(Url), new HttpRequestOptions { Headers = headers.Split('\n') });
    }

    [TestMethod]
    [DataRow("Accept:", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\n", DisplayName = "Remove Accept")]
    [DataRow("Accept:   ", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\n", DisplayName = "Remove Accept, blanks after colon")]
    [DataRow("Accept; y", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\n", DisplayName = "Accept; y removes Accept")]
    [DataRow("Host:", "User-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Remove Host")]
    [DataRow("host:", "User-Agent: curl/8.21.0\r\nAccept: */*\r\n", DisplayName = "Lower-case host: removes Host (upstream test461)")]
    [DataRow("Host:\nHost: x", "User-Agent: curl/8.21.0\r\nAccept: */*\r\nHost: x\r\n", DisplayName = "Removed Host, later Host sent last")]
    [DataRow("X-A:\nX-C:   ", DefaultHeaders, DisplayName = "Removing a header curl does not send")]
    [DataRow("Foo\nX-A: 1", DefaultHeaders + "X-A: 1\r\n", DisplayName = "No colon or semicolon dropped")]
    [DataRow(":x\nX-A: 1", DefaultHeaders + "X-A: 1\r\n", DisplayName = "Empty name before colon dropped")]
    [DataRow(";\nX-A: 1", DefaultHeaders + "X-A: 1\r\n", DisplayName = "Empty name before semicolon dropped")]
    [DataRow("X-B; y", DefaultHeaders, DisplayName = "Value after semicolon dropped")]
    [DataRow("X-B;  ", DefaultHeaders, DisplayName = "Blanks after semicolon dropped")]
    public void Format_RemovingHeader_SendsMeasuredHead(string headers, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(Url), new HttpRequestOptions { Headers = headers.Split('\n') });
    }

    [TestMethod]
    [DataRow("X-B;", DefaultHeaders + "X-B:\r\n", DisplayName = "Custom header sent empty")]
    [DataRow("Accept;\nX-A: 1", "Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nAccept:\r\nX-A: 1\r\n", DisplayName = "Accept sent empty")]
    [DataRow("User-Agent;", "Host: 127.0.0.1:18091\r\nAccept: */*\r\nUser-Agent:\r\n", DisplayName = "User-Agent sent empty")]
    public void Format_EmptyHeader_SendsNameAndColon(string headers, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(Url), new HttpRequestOptions { Headers = headers.Split('\n') });
    }

    [TestMethod]
    public void Format_CustomMethodAndRemovedAccept_SendsMeasuredHead()
    {
        HttpRequestOptions options = new() { CustomMethod = "PUT", Headers = ["Accept:", "X-A: 1"] };

        AssertHead("PUT / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nX-A: 1\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    public void Format_CustomMethod_SentVerbatim()
    {
        AssertHead("get / HTTP/1.1\r\n" + DefaultHeaders + "\r\n", CurlUrl.Parse(Url), new HttpRequestOptions { CustomMethod = "get" });
    }

    [TestMethod]
    [DataRow(null, DefaultHeaders, DisplayName = "Null sends curl/8.21.0")]
    [DataRow("agent/1", "Host: 127.0.0.1:18091\r\nUser-Agent: agent/1\r\nAccept: */*\r\n", DisplayName = "-A agent/1")]
    [DataRow("", "Host: 127.0.0.1:18091\r\nAccept: */*\r\n", DisplayName = "Empty omits it")]
    public void Format_UserAgent_SendsMeasuredHead(string? userAgent, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(Url), new HttpRequestOptions { UserAgent = userAgent });
    }

    [TestMethod]
    [DataRow("a", DisplayName = "-A a -H 'User-Agent: h'")]
    [DataRow("", DisplayName = "-A '' -H 'User-Agent: h'")]
    public void Format_UserAgentAndCustomUserAgent_SendsCustomLast(string userAgent)
    {
        HttpRequestOptions options = new() { UserAgent = userAgent, Headers = ["User-Agent: h"] };

        AssertHead("GET / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nAccept: */*\r\nUser-Agent: h\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    public void Format_UserAgentAndRemovedUserAgent_SendsNone()
    {
        HttpRequestOptions options = new() { UserAgent = "a", Headers = ["User-Agent:"] };

        AssertHead("GET / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nAccept: */*\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    [DataRow("http://r.example/x", DefaultHeaders + "Referer: http://r.example/x\r\n", DisplayName = "-e sends Referer after Accept")]
    [DataRow("", DefaultHeaders, DisplayName = "Empty sends none")]
    [DataRow(null, DefaultHeaders, DisplayName = "Null sends none")]
    public void Format_Referer_SendsMeasuredHead(string? referer, string expectedHeaders)
    {
        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(Url), new HttpRequestOptions { Referer = referer });
    }

    [TestMethod]
    [DataRow("Referer: h", DefaultHeaders + "Referer: h\r\n", DisplayName = "-H Referer replaces -e")]
    [DataRow("Referer:", DefaultHeaders, DisplayName = "-H Referer: removes -e")]
    public void Format_RefererAndCustomReferer_SendsMeasuredHead(string header, string expectedHeaders)
    {
        HttpRequestOptions options = new() { Referer = "http://r/", Headers = [header] };

        AssertHead("GET / HTTP/1.1\r\n" + expectedHeaders + "\r\n", CurlUrl.Parse(Url), options);
    }

    /// <summary>
    /// Measured with curl 8.21.0 <c>--compressed -e http://r/ -H "X-A: 1"</c> (BL-177 Notes):
    /// Accept-Encoding follows Accept and comes before Referer and the <c>-H</c> values, with
    /// all four of the Schannel build's tokens, <c>zstd</c> included (BL-861 Notes, ADR-0287).
    /// </summary>
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Format_Compressed_SendsAcceptEncodingAfterAccept()
    {
        HttpRequestOptions options = new() { Compressed = true, Referer = "http://r/", Headers = ["X-A: 1"] };

        AssertHead(
            "GET / HTTP/1.1\r\n" + DefaultHeaders + "Accept-Encoding: deflate, gzip, br, zstd\r\nReferer: http://r/\r\nX-A: 1\r\n\r\n",
            CurlUrl.Parse(Url),
            options);
    }

    /// <summary>
    /// Measured with Linux curl 8.18.0 (OpenSSL, WSL Ubuntu) <c>--compressed</c> (BL-861
    /// Notes): the OpenSSL build sends the same four tokens as the Schannel build; macOS's
    /// OpenSSL build links brotli and zstd too (cited, ADR-0287).
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Format_Compressed_OnTheOpenSslBuild_SendsTheSameFourTokens()
    {
        AssertHead(
            "GET / HTTP/1.1\r\n" + DefaultHeaders + "Accept-Encoding: deflate, gzip, br, zstd\r\n\r\n",
            CurlUrl.Parse(Url),
            new HttpRequestOptions { Compressed = true });
    }

    [TestMethod]
    public void Format_NotCompressed_SendsNoAcceptEncoding()
    {
        AssertHead("GET / HTTP/1.1\r\n" + DefaultHeaders + "\r\n", CurlUrl.Parse(Url), new HttpRequestOptions { Compressed = false });
    }

    /// <summary>
    /// Measured with curl 8.21.0 <c>--compressed -H "Accept-Encoding: gzip"</c> (BL-177 Notes):
    /// the <c>-H</c> value replaces curl's own.
    /// </summary>
    [TestMethod]
    public void Format_CompressedAndCustomAcceptEncoding_SendsTheCustomOne()
    {
        HttpRequestOptions options = new() { Compressed = true, Headers = ["Accept-Encoding: gzip"] };

        AssertHead("GET / HTTP/1.1\r\n" + DefaultHeaders + "Accept-Encoding: gzip\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    public void Format_NonAsciiHeader_SendsLatin1WithBestFit()
    {
        byte[] expected = [.. Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n" + DefaultHeaders + "X-A: "), 0xE9, .. "\r\nX-B: A\r\n\r\n"u8];

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl(Url), Arranged(new HttpRequestOptions { Headers = ["X-A: \u00E9", "X-B: \u0100"] }));

        Diagnostics.Act("head length", head.Length);
        Diagnostics.Bytes("request head", head);
        Diagnostics.Diff("request head", expected, head);
        CollectionAssert.AreEqual(expected, head);
    }

    /// <summary>
    /// Measured with curl 8.21.0 (mingw) on a Windows-1252 system (ADR-0067, BL-172): <c>é</c>
    /// is <c>E9</c>, U+0100 takes the best fit <c>A</c>, <c>€</c> is Windows-1252's <c>80</c>
    /// and <c>中</c>, with no best fit, is <c>?</c>.
    /// </summary>
    [TestMethod]
    public void Format_NonAsciiHeaderInWindows1252_SendsTheMeasuredBytes()
    {
        byte[] expected =
        [
            .. Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n" + DefaultHeaders + "X-A: "), 0xE9,
            .. "\r\nX-B: "u8, 0x41, .. "\r\nX-C: "u8, 0x80, .. "\r\nX-D: "u8, 0x3F, .. "\r\n\r\n"u8,
        ];
        HttpRequestOptions options = new()
        {
            Headers = ["X-A: é", "X-B: Ā", "X-C: €", "X-D: 中"],
            CommandLineTextEncoding = CodePagesEncodingProvider.Instance.GetEncoding(1252)!,
        };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl(Url), Arranged(options));

        Diagnostics.Act("head length", head.Length);
        Diagnostics.Bytes("request head", head);
        Diagnostics.Diff("request head", expected, head);
        CollectionAssert.AreEqual(expected, head);
    }

    /// <summary>
    /// curl on Linux and macOS sends the UTF-8 bytes its shell passed (ADR-0067), for
    /// <c>-H</c>, <c>--proxy-header</c>, <c>-A</c> and <c>-e</c> alike; the request target
    /// stays as the URL has it.
    /// </summary>
    [TestMethod]
    public void Format_NonAsciiCommandLineTextInUtf8_SendsUtf8Bytes()
    {
        HttpRequestOptions options = new()
        {
            Headers = ["X-A: é"],
            ProxyHeaders = ["X-P: é"],
            UserAgent = "é",
            Referer = "€",
            CommandLineTextEncoding = Encoding.UTF8,
        };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://example.com/"), Arranged(options), forwardProxy: true);

        Assert.AreEqual(
            ExpectedHead(
                "GET http://example.com/ HTTP/1.1\r\nHost: example.com\r\nUser-Agent: Ã©\r\nAccept: */*\r\n"
                + "Referer: â\u0082¬\r\nProxy-Connection: Keep-Alive\r\nX-A: Ã©\r\nX-P: Ã©\r\n\r\n",
                head),
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_DefaultOptions_MatchesNullOptions()
    {
        byte[] fromNull = HttpRequestHeadFormatter.Format(ArrangedUrl(Url), Arranged(null));
        byte[] fromDefault = HttpRequestHeadFormatter.Format(CurlUrl.Parse(Url), Arranged(new HttpRequestOptions()));

        Diagnostics.Act("head length", fromNull.Length);
        Diagnostics.Bytes("head from null options", fromNull);
        Diagnostics.Bytes("head from default options", fromDefault);
        Diagnostics.Diff("request head", fromNull, fromDefault);
        CollectionAssert.AreEqual(fromNull, fromDefault);
    }

    [TestMethod]
    [DataRow("x=1", "", "POST / HTTP/1.1\r\n" + DefaultHeaders + "Content-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n", DisplayName = "-d x=1")]
    [DataRow("", "", "POST / HTTP/1.1\r\n" + DefaultHeaders + "Content-Length: 0\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n", DisplayName = "-d ''")]
    [DataRow("x=1", "X-A: b", "POST / HTTP/1.1\r\n" + DefaultHeaders + "X-A: b\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n", DisplayName = "-H before Content-Length")]
    [DataRow("x=1", "Content-Type: text/plain", "POST / HTTP/1.1\r\n" + DefaultHeaders + "Content-Type: text/plain\r\nContent-Length: 3\r\n\r\n", DisplayName = "-H Content-Type replaces")]
    [DataRow("x=1", "Content-Type:\nContent-Length: 3", "POST / HTTP/1.1\r\n" + DefaultHeaders + "Content-Length: 3\r\n\r\n", DisplayName = "-H removes Content-Type, replaces Content-Length")]
    [DataRow("x=1", "Expect: 100-continue", "POST / HTTP/1.1\r\n" + DefaultHeaders + "Expect: 100-continue\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n", DisplayName = "-H Expect in its -H slot")]
    [DataRow("x=1", "Transfer-Encoding: chunked", "POST / HTTP/1.1\r\n" + DefaultHeaders + "Transfer-Encoding: chunked\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n", DisplayName = "-H chunked drops Content-Length")]
    public void Format_FormBody_SendsMeasuredHead(string content, string headers, string expected)
    {
        HttpRequestOptions options = new()
        {
            Headers = headers.Length == 0 ? [] : headers.Split('\n'),
            Body = new BytesBody(Encoding.ASCII.GetBytes(content), "application/x-www-form-urlencoded"),
        };

        AssertHead(expected, CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    public void Format_JsonBody_SendsMeasuredContentTypeAndAccept()
    {
        // curl --json {} -H 'X-A: b': the command-line layer appends --json's two headers after every -H.
        HttpRequestOptions options = new()
        {
            Headers = ["X-A: b", "Content-Type: application/json", "Accept: application/json"],
            Body = new BytesBody("{}"u8.ToArray(), "application/json"),
        };

        AssertHead(
            "POST / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nX-A: b\r\nContent-Type: application/json\r\nAccept: application/json\r\nContent-Length: 2\r\n\r\n",
            CurlUrl.Parse(Url),
            options);
    }

    [TestMethod]
    [DataRow(1048576, "", DisplayName = "1 MiB sends no Expect")]
    [DataRow(1048577, "Expect: 100-continue\r\n", DisplayName = "1 MiB + 1 sends Expect")]
    public void Format_LargeBody_SendsExpectAboveTheMeasuredThreshold(int length, string expect)
    {
        HttpRequestOptions options = new() { Body = new BytesBody(new byte[length], "application/x-www-form-urlencoded") };

        AssertHead(
            $"POST / HTTP/1.1\r\n{DefaultHeaders}Content-Length: {length}\r\nContent-Type: application/x-www-form-urlencoded\r\n{expect}\r\n",
            CurlUrl.Parse(Url),
            options);
    }

    [TestMethod]
    public void Format_LargeBodyWithExpectRemoved_SendsNoExpect()
    {
        HttpRequestOptions options = new() { Headers = ["Expect:"], Body = new BytesBody(new byte[1048577], "a/b") };

        AssertHead($"POST / HTTP/1.1\r\n{DefaultHeaders}Content-Length: 1048577\r\nContent-Type: a/b\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    public void Format_StreamBodyOfUnknownLength_SendsChunkedAndExpect()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(new MemoryStream(), null, "text/plain") };

        AssertHead(
            $"POST / HTTP/1.1\r\n{DefaultHeaders}Transfer-Encoding: chunked\r\nContent-Type: text/plain\r\nExpect: 100-continue\r\n\r\n",
            CurlUrl.Parse(Url),
            options);
    }

    [TestMethod]
    public void Format_StreamBodyOfKnownLength_SendsContentLength()
    {
        HttpRequestOptions options = new() { Body = new StreamBody(new MemoryStream(), 100207, "multipart/form-data; boundary=b") };

        AssertHead(
            $"POST / HTTP/1.1\r\n{DefaultHeaders}Content-Length: 100207\r\nContent-Type: multipart/form-data; boundary=b\r\n\r\n",
            CurlUrl.Parse(Url),
            options);
    }

    [TestMethod]
    [DataRow("Content-Type: text/info", "text/info", DisplayName = "upstream test277")]
    [DataRow("Content-type: multipart/form-data; charset=utf-8", "multipart/form-data; charset=utf-8", DisplayName = "upstream test669")]
    public void Format_FormBodyWithCustomContentType_SendsThatTypeWithTheBoundaryAfterContentLength(string header, string userType)
    {
        HttpRequestOptions options = new()
        {
            Headers = ["X-A: 1", header, "Content-Type: second"],
            Body = new StreamBody(new MemoryStream(), 158, "multipart/form-data; boundary=b"),
        };

        AssertHead(
            $"POST / HTTP/1.1\r\n{DefaultHeaders}X-A: 1\r\nContent-Length: 158\r\nContent-Type: {userType}; boundary=b\r\n\r\n",
            CurlUrl.Parse(Url),
            options);
    }

    [TestMethod]
    public void Format_FormBodyWithEmptyCustomContentType_SendsNoContentType()
    {
        HttpRequestOptions options = new() { Headers = ["Content-Type:"], Body = new StreamBody(new MemoryStream(), 158, "multipart/form-data; boundary=b") };

        AssertHead($"POST / HTTP/1.1\r\n{DefaultHeaders}Content-Length: 158\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    public void Format_NonFormBodyWithCustomContentType_SendsTheCustomLineUnchanged()
    {
        HttpRequestOptions options = new() { Headers = ["Content-Type: text/info"], Body = new StreamBody(new MemoryStream(), 3, "text/plain") };

        AssertHead($"POST / HTTP/1.1\r\n{DefaultHeaders}Content-Type: text/info\r\nContent-Length: 3\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    public void Format_BodyWithCustomMethodAndNoContentType_SendsThatMethodAndNoContentType()
    {
        HttpRequestOptions options = new() { CustomMethod = "PUT", Body = new BytesBody("x=1"u8.ToArray(), string.Empty) };

        AssertHead($"PUT / HTTP/1.1\r\n{DefaultHeaders}Content-Length: 3\r\n\r\n", CurlUrl.Parse(Url), options);
    }

    [TestMethod]
    [DataRow(null, "HEAD", DisplayName = "-I")]
    [DataRow("GET", "GET", DisplayName = "-I -X GET")]
    public void Format_NoBody_SendsTheMeasuredHead(string? customMethod, string method)
    {
        HttpRequestOptions options = new() { CustomMethod = customMethod };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://127.0.0.1:18276/a?b"), Arranged(options), noBody: true);

        Assert.AreEqual(
            ExpectedHead(method + " /a?b HTTP/1.1\r\nHost: 127.0.0.1:18276\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", head),
            Encoding.Latin1.GetString(head));
    }

    /// <summary>
    /// Measured (BL-181 Notes): <c>curl -u u:p</c> sends <c>Authorization</c> straight after
    /// <c>Host</c>, a custom <c>Host</c> included, and an <c>-H Authorization</c> value
    /// replaces it in the custom headers' place.
    /// </summary>
    [TestMethod]
    [DataRow(null, "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18181\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "-u u:p")]
    [DataRow("Host: h", "GET /a HTTP/1.1\r\nHost: h\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n", DisplayName = "-u u:p -H Host")]
    [DataRow("Authorization: X y", "GET /a HTTP/1.1\r\nHost: 127.0.0.1:18181\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAuthorization: X y\r\n\r\n", DisplayName = "-u u:p -H Authorization")]
    public void Format_Authorization_SendsItAfterHost(string? header, string expected)
    {
        HttpRequestOptions options = new() { Headers = header is null ? [] : [header] };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://127.0.0.1:18181/a"), Arranged(options), authorization: "Basic dTpw");

        Assert.AreEqual(ExpectedHead(expected, head), Encoding.Latin1.GetString(head));
    }

    /// <summary>
    /// Measured (BL-986 Notes): beside an <c>-H Authorization</c> value, curl still sends its
    /// Digest and NTLM values after <c>Host</c>, and Negotiate too as libcurl's
    /// <c>output_auth_headers</c> checks the custom headers for Basic and Bearer alone; Basic,
    /// Bearer and an <c>--aws-sigv4</c> value are dropped.
    /// </summary>
    [TestMethod]
    [DataRow("Digest username=\"u\"", true, DisplayName = "Digest")]
    [DataRow("NTLM dHlwZTE=", true, DisplayName = "NTLM")]
    [DataRow("Negotiate dG9rZW4=", true, DisplayName = "Negotiate")]
    [DataRow("Basic dTpw", false, DisplayName = "Basic")]
    [DataRow("Bearer tok", false, DisplayName = "Bearer")]
    [DataRow("AWS4-HMAC-SHA256 Credential=k", false, DisplayName = "AWS SigV4")]
    [DataRow("NTLMish", false, DisplayName = "Scheme only starting NTLM")]
    public void Format_AuthorizationBesideAnAuthorizationHeader_SendsOnlyDigestNtlmAndNegotiate(string authorization, bool sent)
    {
        HttpRequestOptions options = new() { Headers = ["Authorization: x"] };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://127.0.0.1:18181/a"), Arranged(options), authorization: authorization);

        string ownLine = sent ? $"Authorization: {authorization}\r\n" : string.Empty;
        Assert.AreEqual(
            ExpectedHead($"GET /a HTTP/1.1\r\nHost: 127.0.0.1:18181\r\n{ownLine}User-Agent: curl/8.21.0\r\nAccept: */*\r\nAuthorization: x\r\n\r\n", head),
            Encoding.Latin1.GetString(head));
    }

    /// <summary>
    /// Measured (BL-182 Notes): with a cookie jar, curl sends <c>Cookie</c> after
    /// <c>Referer</c> and before the <c>-H</c> values and the body's headers, and still sends
    /// it when an <c>-H</c> value names <c>Cookie</c>. The <c>--compressed</c> row sends
    /// <c>Accept-Encoding</c> with <c>zstd</c> (ADR-0287).
    /// </summary>
    [TestMethod]
    [DataRow(
        new[] { "X-A: 1" },
        null,
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAccept-Encoding: deflate, gzip, br, zstd\r\nReferer: ref\r\nCookie: j=k\r\nX-A: 1\r\n\r\n",
        DisplayName = "-u -e --compressed -H")]
    [DataRow(
        new[] { "Cookie: c=d" },
        null,
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: j=k\r\nCookie: c=d\r\n\r\n",
        DisplayName = "-H Cookie: c=d")]
    [DataRow(
        new[] { "Cookie:" },
        null,
        "GET / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: j=k\r\n\r\n",
        DisplayName = "-H Cookie:")]
    [DataRow(
        new string[0],
        "x=1",
        "POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nCookie: j=k\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\n",
        DisplayName = "-d x=1")]
    public void Format_Cookie_SendsItAfterRefererAndBeforeTheCustomHeaders(string[] headers, string? body, string expected)
    {
        bool full = headers.Contains("X-A: 1");
        HttpRequestOptions options = new()
        {
            Headers = headers,
            Referer = full ? "ref" : null,
            Compressed = full,
            Body = body is null ? null : new BytesBody(Encoding.Latin1.GetBytes(body), "application/x-www-form-urlencoded"),
        };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://127.0.0.1:18082/"), Arranged(options), authorization: full ? "Basic dTpw" : null, cookie: "j=k");

        Assert.AreEqual(ExpectedHead(expected, head), Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_EmptyCookie_SendsNoCookieHeader()
    {
        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl(Url), Arranged(null), cookie: string.Empty);

        Assert.AreEqual(ExpectedHead("GET / HTTP/1.1\r\n" + DefaultHeaders + "\r\n", head), Encoding.Latin1.GetString(head));
    }

    /// <summary>
    /// Measured through <c>-x http://127.0.0.1:18183</c> (BL-183 Notes): a <c>-H</c> value naming
    /// <c>Proxy-Connection</c> replaces or removes curl's, in any letter case, while curl's
    /// <c>Proxy-Authorization</c> is sent beside a custom one.
    /// </summary>
    [TestMethod]
    [DataRow(
        "http://example.com",
        new[] { "Proxy-Connection: close", "Proxy-Authorization: X" },
        "Basic dTpw",
        "GET http://example.com/ HTTP/1.1\r\nHost: example.com\r\nProxy-Authorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Proxy-Connection: close\r\nProxy-Authorization: X\r\n\r\n",
        DisplayName = "-H Proxy-Connection and Proxy-Authorization")]
    [DataRow(
        "http://example.com/",
        new[] { "Proxy-Connection:" },
        null,
        "GET http://example.com/ HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
        DisplayName = "-H Proxy-Connection: removes it")]
    [DataRow(
        "http://Example.COM",
        new[] { "proxy-connection: x" },
        null,
        "GET http://Example.COM/ HTTP/1.1\r\nHost: Example.COM\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nproxy-connection: x\r\n\r\n",
        DisplayName = "-H proxy-connection in lower case")]
    [DataRow(
        "http://example.com:8080/p",
        new[] { "X-A: 1" },
        "Basic cHU6cHA=",
        "GET http://example.com:8080/p HTTP/1.1\r\nHost: example.com:8080\r\nProxy-Authorization: Basic cHU6cHA=\r\nUser-Agent: curl/8.21.0\r\n"
            + "Accept: */*\r\nProxy-Connection: Keep-Alive\r\nX-A: 1\r\n\r\n",
        DisplayName = "Port kept, -H after Proxy-Connection")]
    public void Format_ForwardProxy_SendsMeasuredHead(string url, string[] headers, string? proxyAuthorization, string expected)
    {
        HttpRequestOptions options = new() { Headers = headers };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl(url), Arranged(options), forwardProxy: true, proxyAuthorization: proxyAuthorization);

        Assert.AreEqual(ExpectedHead(expected, head), Encoding.Latin1.GetString(head));
    }

    /// <summary>
    /// Measured through <c>-x http://127.0.0.1:18296</c> (BL-296 Notes): <c>--proxy-header</c>
    /// values follow the <c>-H</c> values under the same rules, and override only curl's
    /// <c>Proxy-Connection</c>; a <c>Host</c> line already written keeps theirs out.
    /// </summary>
    [TestMethod]
    [DataRow(
        new[] { "X-A: 1" },
        new[] { "X-P: 1" },
        "Host: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\nX-A: 1\r\nX-P: 1\r\n",
        DisplayName = "-H then --proxy-header")]
    [DataRow(
        new[] { "X-A: 1" },
        new[] { "Proxy-Connection: close" },
        "Host: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nX-A: 1\r\nProxy-Connection: close\r\n",
        DisplayName = "--proxy-header Proxy-Connection replaces curl's")]
    [DataRow(
        new[] { "Proxy-Connection: close" },
        new[] { "X-P: 1" },
        "Host: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: close\r\nX-P: 1\r\n",
        DisplayName = "-H Proxy-Connection still replaces curl's")]
    [DataRow(
        new string[0],
        new[] { "User-Agent: pu", "X-E:", "X-S;", "Host: ph" },
        "Host: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\nUser-Agent: pu\r\nX-S:\r\n",
        DisplayName = "--proxy-header forms, no override but Proxy-Connection")]
    [DataRow(
        new[] { "Host:" },
        new[] { "Host: ph" },
        "User-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\nHost: ph\r\n",
        DisplayName = "--proxy-header Host sent when -H Host: removes curl's")]
    [DataRow(
        new string[0],
        new[] { "Proxy-Connection:", "Authorization: z" },
        "Host: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAuthorization: z\r\n",
        DisplayName = "--proxy-header Proxy-Connection: removes curl's")]
    public void Format_ForwardProxyWithProxyHeaders_SendsMeasuredHead(string[] headers, string[] proxyHeaders, string expectedHeaders)
    {
        HttpRequestOptions options = new() { Headers = headers, ProxyHeaders = proxyHeaders };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://example.com/"), Arranged(options), forwardProxy: true);

        Assert.AreEqual(ExpectedHead("GET http://example.com/ HTTP/1.1\r\n" + expectedHeaders + "\r\n", head), Encoding.Latin1.GetString(head));
    }

    /// <summary>
    /// Measured: <c>curl -x http://127.0.0.1:18296 --proxy-header "Content-Type: x"
    /// --proxy-header "Content-Length: 9" -d xy http://example.com/</c> sends both before
    /// curl's own body headers, which they do not override (BL-296 Notes).
    /// </summary>
    [TestMethod]
    public void Format_ForwardProxyWithProxyHeadersAndBody_SendsThemBeforeTheBodyHeaders()
    {
        const string expected = "POST http://example.com/ HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Proxy-Connection: Keep-Alive\r\nContent-Type: x\r\nContent-Length: 9\r\nContent-Length: 2\r\n"
            + "Content-Type: application/x-www-form-urlencoded\r\n\r\n";
        HttpRequestOptions options = new()
        {
            ProxyHeaders = ["Content-Type: x", "Content-Length: 9"],
            Body = new BytesBody("xy"u8.ToArray(), "application/x-www-form-urlencoded"),
        };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://example.com/"), Arranged(options), forwardProxy: true);

        Assert.AreEqual(ExpectedHead(expected, head), Encoding.Latin1.GetString(head));
    }

    /// <summary>
    /// Measured: <c>curl --proxy-header "X-P: 1" http://127.0.0.1:18296/</c> sends the default
    /// head: a request to the origin never carries <c>--proxy-header</c> values, and none of
    /// them overrides a header curl sends there.
    /// </summary>
    [TestMethod]
    public void Format_ProxyHeadersWithoutForwardProxy_SendsNone()
    {
        AssertHead(
            "GET / HTTP/1.1\r\n" + DefaultHeaders + "\r\n",
            CurlUrl.Parse(Url),
            new HttpRequestOptions { ProxyHeaders = ["X-P: 1", "Host: ph", "User-Agent: x"] });
    }

    /// <summary>
    /// Measured: <c>curl -x http://127.0.0.1:18183 -H "Host: other" -b a=b -e r --compressed
    /// http://example.com/h</c> sends <c>Proxy-Connection</c> after <c>Referer</c> and before
    /// <c>Cookie</c>; <c>Accept-Encoding</c> leaves out <c>zstd</c> (ADR-0020).
    /// </summary>
    [TestMethod]
    public void Format_ForwardProxyWithRefererAndCookie_SendsProxyConnectionBetweenThem()
    {
        const string expected = "GET http://example.com/h HTTP/1.1\r\nHost: other\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n"
            + "Accept-Encoding: deflate, gzip, br, zstd\r\nReferer: r\r\nProxy-Connection: Keep-Alive\r\nCookie: a=b\r\n\r\n";
        HttpRequestOptions options = new() { Headers = ["Host: other"], Referer = "r", Compressed = true };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://example.com/h"), Arranged(options), cookie: "a=b", forwardProxy: true);

        Assert.AreEqual(ExpectedHead(expected, head), Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_Http10_EndsTheRequestLineInHttp10WithTheSameHeaders()
    {
        AssertHead(
            "GET /a HTTP/1.0\r\n" + DefaultHeaders + "\r\n",
            CurlUrl.Parse("http://127.0.0.1:18091/a"),
            new HttpRequestOptions { Version = HttpVersionPreference.Http10 });
    }

    [TestMethod]
    public void Format_Http10BodyAboveTheThreshold_SendsNoExpect()
    {
        HttpRequestOptions options = new()
        {
            Version = HttpVersionPreference.Http10,
            CustomMethod = "PUT",
            Body = new BytesBody(new byte[1048577], "application/octet-stream"),
            Headers = ["Content-Type:"],
        };

        AssertHead(
            "PUT /a HTTP/1.0\r\n" + DefaultHeaders + "Content-Length: 1048577\r\n\r\n",
            CurlUrl.Parse("http://127.0.0.1:18091/a"),
            options);
    }

    /// <summary>
    /// Measured: <c>curl --request-target /x/../y?z http://127.0.0.1:18189/a</c> sends the
    /// target as given, dot segments and all (BL-186 Notes).
    /// </summary>
    [TestMethod]
    public void Format_RequestTarget_ReplacesThePathAndQueryVerbatim()
    {
        AssertHead(
            "GET /x/../y?z HTTP/1.1\r\nHost: 127.0.0.1:18189\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n",
            CurlUrl.Parse("http://127.0.0.1:18189/a"),
            new HttpRequestOptions { RequestTarget = "/x/../y?z" });
    }

    /// <summary>
    /// Measured: <c>curl -x http://127.0.0.1:18190 --request-target * -X OPTIONS
    /// http://example.com/a</c> sends <c>OPTIONS * HTTP/1.1</c> to the proxy: the target
    /// replaces the absolute form too (BL-186 Notes).
    /// </summary>
    [TestMethod]
    public void Format_RequestTargetThroughForwardProxy_ReplacesTheAbsoluteForm()
    {
        HttpRequestOptions options = new() { CustomMethod = "OPTIONS", RequestTarget = "*" };

        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl("http://example.com/a"), Arranged(options), forwardProxy: true);

        Assert.AreEqual(
            ExpectedHead("OPTIONS * HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nProxy-Connection: Keep-Alive\r\n\r\n", head),
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_RequestTargetAboveAscii_SendsItsUtf8Bytes()
    {
        byte[] head = HttpRequestHeadFormatter.Format(ArrangedUrl(Url), Arranged(new HttpRequestOptions { RequestTarget = "/ä" }));

        byte[] expected = [.. "GET /"u8, 0xC3, 0xA4, .. " HTTP/1.1\r\n"u8];
        Diagnostics.Act("head length", head.Length);
        Diagnostics.Bytes("request line", head[..expected.Length]);
        Diagnostics.Diff("request line", expected, head[..expected.Length]);
        CollectionAssert.AreEqual(expected, head[..expected.Length]);
    }

    private void AssertHead(string expected, CurlUrl url, HttpRequestOptions? options)
    {
        Diagnostics.Arrange("URL", url.OriginalString);
        byte[] head = HttpRequestHeadFormatter.Format(url, Arranged(options));
        Assert.AreEqual(ExpectedHead(expected, head), Encoding.Latin1.GetString(head));
    }

    private CurlUrl ArrangedUrl(string url)
    {
        Diagnostics.Arrange("URL", url);
        return CurlUrl.Parse(url);
    }

    private HttpRequestOptions? Arranged(HttpRequestOptions? options)
    {
        Diagnostics.Arrange("options", HttpRequestOptionsDescription.Of(options));
        return options;
    }

    private string ExpectedHead(string expected, byte[] head)
    {
        string actual = Encoding.Latin1.GetString(head);
        Diagnostics.Act("request line", actual[..Math.Max(0, actual.IndexOf("\r\n", StringComparison.Ordinal))]);
        Diagnostics.Bytes("request head", head);
        Diagnostics.Diff("request head", expected, actual);
        return expected;
    }
}
