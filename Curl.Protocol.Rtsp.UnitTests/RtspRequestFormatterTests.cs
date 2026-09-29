using System.Text;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Pins the RTSP request head against curl 8.21.0, measured on 2026-09-28 against a loopback
/// listener (ADR-0169, BL-591 Notes): header order, <c>-H</c> overrides and removals.
/// </summary>
[TestClass]
public sealed class RtspRequestFormatterTests
{
    [TestMethod]
    public void Format_NoOptions_WritesOptionsStarWithCSeqAndCurlsUserAgent()
    {
        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n",
            Format(new HttpRequestOptions()));
    }

    [TestMethod]
    public void Format_AgentAndHeader_WritesTheAgentThenTheHeader()
    {
        var options = new HttpRequestOptions { UserAgent = "agent/1", Headers = ["X-Test: 1"] };

        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: agent/1\r\nX-Test: 1\r\n\r\n",
            Format(options));
    }

    [TestMethod]
    public void Format_Authorization_WritesItAfterTheUserAgent()
    {
        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nAuthorization: Basic dTpw\r\n\r\n",
            Format(new HttpRequestOptions(), authorization: "Basic dTpw"));
    }

    [TestMethod]
    public void Format_Referer_WritesItBetweenCSeqAndUserAgent()
    {
        var options = new HttpRequestOptions { Referer = "http://r/", Headers = ["Accept: application/sdp"] };

        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nReferer: http://r/\r\nUser-Agent: curl/8.21.0\r\nAccept: application/sdp\r\n\r\n",
            Format(options));
    }

    [TestMethod]
    public void Format_HeadersNamingCurlsOwn_SuppressCurlsAndAreSentInCommandLineOrder()
    {
        var options = new HttpRequestOptions { Referer = "http://r/", Headers = ["X-A: 1", "User-Agent: mine"] };

        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nReferer: http://r/\r\nAuthorization: Basic dTpw\r\nX-A: 1\r\nUser-Agent: mine\r\n\r\n",
            Format(options, authorization: "Basic dTpw"));
    }

    [TestMethod]
    public void Format_CustomAuthorization_ReplacesTheAuthenticatorsValue()
    {
        var options = new HttpRequestOptions { Headers = ["Authorization: Bearer t"] };

        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nAuthorization: Bearer t\r\n\r\n",
            Format(options, authorization: "Basic dTpw"));
    }

    [TestMethod]
    public void Format_EmptyUserAgentHeader_RemovesTheUserAgent()
    {
        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n",
            Format(new HttpRequestOptions { Headers = ["User-Agent:"] }));
    }

    [TestMethod]
    public void Format_SemicolonAndEmptyHeaders_SendsAnEmptyValueAndNothing()
    {
        var options = new HttpRequestOptions { Referer = "http://r/", Headers = ["X-Empty;", "X-Gone:", "Referer: other"] };

        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nX-Empty:\r\nReferer: other\r\n\r\n",
            Format(options));
    }

    [TestMethod]
    public void Format_HeaderWithoutSeparator_SendsNothing()
    {
        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n",
            Format(new HttpRequestOptions { Headers = ["Bare", ":x", ";", "X;y"] }));
    }

    [TestMethod]
    public void Format_Session_WritesItAfterCSeq()
    {
        byte[] head = RtspRequestFormatter.Format(RtspMethod.Options, "*", 2, "1234", new HttpRequestOptions(), null);

        Assert.AreEqual(
            "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nSession: 1234\r\nUser-Agent: curl/8.21.0\r\n\r\n",
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_CommandLineTextEncoding_EncodesAgentRefererAndHeadersWithIt()
    {
        var options = new HttpRequestOptions
        {
            UserAgent = "é",
            Referer = "http://r/é",
            Headers = ["X: é"],
            CommandLineTextEncoding = Encoding.UTF8,
        };

        byte[] head = RtspRequestFormatter.Format(RtspMethod.Options, "*", 1, null, options, null);

        CollectionAssert.AreEqual(
            Encoding.UTF8.GetBytes("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nReferer: http://r/é\r\nUser-Agent: é\r\nX: é\r\n\r\n"),
            head);
    }

    [TestMethod]
    [DataRow("CSeq: 5")]
    [DataRow("CSeq:")]
    [DataRow("cseq;")]
    public void NamesCSeq_HeaderNamingCSeq_IsTrue(string header)
    {
        Assert.IsTrue(RtspRequestFormatter.NamesCSeq(new HttpRequestOptions { Headers = ["X: 1", header] }));
    }

    [TestMethod]
    [DataRow("CSeqX: 5")]
    [DataRow("X-CSeq: 5")]
    [DataRow("CSeq")]
    public void NamesCSeq_OtherHeader_IsFalse(string header)
    {
        Assert.IsFalse(RtspRequestFormatter.NamesCSeq(new HttpRequestOptions { Headers = [header] }));
    }

    [TestMethod]
    public void Options_IsNamedOptions()
    {
        Assert.AreEqual("OPTIONS", RtspMethod.Options.Name);
    }

    private static string Format(HttpRequestOptions options, string? authorization = null) =>
        Encoding.Latin1.GetString(RtspRequestFormatter.Format(RtspMethod.Options, "*", 1, null, options, authorization));
}
