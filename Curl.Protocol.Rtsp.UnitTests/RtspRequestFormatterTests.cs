using System.Text;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Pins the RTSP request head against curl 8.21.0, measured on 2026-09-28 against a loopback
/// listener (ADR-0169, BL-591 Notes): header order, <c>-H</c> overrides and removals.
/// </summary>
[TestClass]
public sealed class RtspRequestFormatterTests
{
    /// <summary>Gets or sets the running test's context, which carries its diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Format_NoOptions_WritesOptionsStarWithCSeqAndCurlsUserAgent()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n";

        string head = Format(new HttpRequestOptions());

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_AgentAndHeader_WritesTheAgentThenTheHeader()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: agent/1\r\nX-Test: 1\r\n\r\n";
        var options = new HttpRequestOptions { UserAgent = "agent/1", Headers = ["X-Test: 1"] };

        string head = Format(options);

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_Authorization_WritesItAfterTheUserAgent()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nAuthorization: Basic dTpw\r\n\r\n";

        string head = Format(new HttpRequestOptions(), authorization: "Basic dTpw");

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_Referer_WritesItBetweenCSeqAndUserAgent()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nReferer: http://r/\r\nUser-Agent: curl/8.21.0\r\nAccept: application/sdp\r\n\r\n";
        var options = new HttpRequestOptions { Referer = "http://r/", Headers = ["Accept: application/sdp"] };

        string head = Format(options);

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_HeadersNamingCurlsOwn_SuppressCurlsAndAreSentInCommandLineOrder()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nReferer: http://r/\r\nAuthorization: Basic dTpw\r\nX-A: 1\r\nUser-Agent: mine\r\n\r\n";
        var options = new HttpRequestOptions { Referer = "http://r/", Headers = ["X-A: 1", "User-Agent: mine"] };

        string head = Format(options, authorization: "Basic dTpw");

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_CustomAuthorization_ReplacesTheAuthenticatorsValue()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nAuthorization: Bearer t\r\n\r\n";
        var options = new HttpRequestOptions { Headers = ["Authorization: Bearer t"] };

        string head = Format(options, authorization: "Basic dTpw");

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_EmptyUserAgentHeader_RemovesTheUserAgent()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\n\r\n";

        string head = Format(new HttpRequestOptions { Headers = ["User-Agent:"] });

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_SemicolonAndEmptyHeaders_SendsAnEmptyValueAndNothing()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\nX-Empty:\r\nReferer: other\r\n\r\n";
        var options = new HttpRequestOptions { Referer = "http://r/", Headers = ["X-Empty;", "X-Gone:", "Referer: other"] };

        string head = Format(options);

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_HeaderWithoutSeparator_SendsNothing()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n\r\n";

        string head = Format(new HttpRequestOptions { Headers = ["Bare", ":x", ";", "X;y"] });

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_Session_WritesItAfterCSeq()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nSession: 1234\r\nUser-Agent: curl/8.21.0\r\n\r\n";
        Diagnostics.Arrange("method, target, CSeq, session, range", "OPTIONS, *, 2, 1234, (none)");

        byte[] head = RtspRequestFormatter.Format(RtspMethod.Options, "*", 2, "1234", null, new HttpRequestOptions(), null);

        Diagnostics.ActSent(head);
        Diagnostics.DiffText("request head", expected, head);
        Assert.AreEqual(
            expected,
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_RangeAndReferer_WritesRangeBeforeReferer()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nRange: 1-2\r\nReferer: http://r/\r\nUser-Agent: curl/8.21.0\r\n\r\n";
        var options = new HttpRequestOptions { Referer = "http://r/" };

        string head = Format(options, range: "1-2");

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    public void Format_SessionAndRange_WritesSessionBeforeRange()
    {
        const string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 2\r\nSession: 1234\r\nRange: 5-\r\nUser-Agent: curl/8.21.0\r\n\r\n";
        Diagnostics.Arrange("method, target, CSeq, session, range", "OPTIONS, *, 2, 1234, 5-");

        byte[] head = RtspRequestFormatter.Format(RtspMethod.Options, "*", 2, "1234", "5-", new HttpRequestOptions(), null);

        Diagnostics.ActSent(head);
        Diagnostics.DiffText("request head", expected, head);
        Assert.AreEqual(
            expected,
            Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    [DataRow("Range: npt=0-")]
    [DataRow("range: npt=0-")]
    [DataRow("RANGE: npt=0-")]
    public void Format_RangeHeaderOfAnyCase_SuppressesCurlsRange(string header)
    {
        string expected = "OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nUser-Agent: curl/8.21.0\r\n" + header + "\r\n\r\n";
        var options = new HttpRequestOptions { Headers = [header] };

        string head = Format(options, range: "1-2");

        Diagnostics.Diff("request head", expected, head);
        Assert.AreEqual(expected, head);
    }

    [TestMethod]
    [DataRow(null, null, null)]
    [DataRow(0L, null, null)]
    [DataRow(0L, "1-2", "1-2")]
    [DataRow(null, "1-2", "1-2")]
    [DataRow(5L, null, "5-")]
    [DataRow(5L, "1-2", "5-")]
    public void RangeValue_ResumeAndRange_GivesCurlsRangeValue(long? resumeFrom, string? rangeText, string? expected)
    {
        Diagnostics.Arrange("resume from", resumeFrom?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        Diagnostics.Arrange("range", rangeText ?? "(none)");

        string? value = RtspRequestFormatter.RangeValue(resumeFrom, rangeText);

        Diagnostics.Act("range value", value ?? "(none)");
        Diagnostics.Assert("range value", expected ?? "(none)", value ?? "(none)");
        Assert.AreEqual(expected, value);
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
        byte[] expected = Encoding.UTF8.GetBytes("OPTIONS * RTSP/1.0\r\nCSeq: 1\r\nReferer: http://r/é\r\nUser-Agent: é\r\nX: é\r\n\r\n");
        Diagnostics.Arrange("command-line text encoding", "UTF-8");
        Diagnostics.Arrange("user agent, referer, header", "é, http://r/é, X: é");

        byte[] head = RtspRequestFormatter.Format(RtspMethod.Options, "*", 1, null, null, options, null);

        Diagnostics.ActSent(head);
        Diagnostics.Diff("request head", expected, head);
        CollectionAssert.AreEqual(
            expected,
            head);
    }

    [TestMethod]
    [DataRow("CSeq: 5")]
    [DataRow("CSeq:")]
    [DataRow("cseq;")]
    public void NamesCSeq_HeaderNamingCSeq_IsTrue(string header)
    {
        Diagnostics.Arrange("headers", "X: 1 | " + header);

        bool namesCSeq = RtspRequestFormatter.NamesCSeq(new HttpRequestOptions { Headers = ["X: 1", header] });

        Diagnostics.Act("names CSeq", namesCSeq);
        Diagnostics.Assert("names CSeq", true, namesCSeq);
        Assert.IsTrue(namesCSeq);
    }

    [TestMethod]
    [DataRow("CSeqX: 5")]
    [DataRow("X-CSeq: 5")]
    [DataRow("CSeq")]
    public void NamesCSeq_OtherHeader_IsFalse(string header)
    {
        Diagnostics.Arrange("headers", header);

        bool namesCSeq = RtspRequestFormatter.NamesCSeq(new HttpRequestOptions { Headers = [header] });

        Diagnostics.Act("names CSeq", namesCSeq);
        Diagnostics.Assert("names CSeq", false, namesCSeq);
        Assert.IsFalse(namesCSeq);
    }

    [TestMethod]
    public void Options_IsNamedOptions()
    {
        Diagnostics.Arrange("method", nameof(RtspMethod.Options));

        string name = RtspMethod.Options.Name;

        Diagnostics.Act("name", name);
        Diagnostics.Assert("name", "OPTIONS", name);
        Assert.AreEqual("OPTIONS", name);
    }

    /// <summary>Formats an OPTIONS * request with CSeq 1, writing its inputs and the head it gets.</summary>
    private string Format(HttpRequestOptions options, string? authorization = null, string? range = null)
    {
        Diagnostics.Arrange("method, target, CSeq", "OPTIONS, *, 1");
        Diagnostics.Arrange("headers", options.Headers.Count == 0 ? "(none)" : RtspDiagnostics.Text(string.Join(" | ", options.Headers)));
        Diagnostics.Arrange("user agent", options.UserAgent ?? "(default)");
        Diagnostics.Arrange("referer", options.Referer ?? "(none)");
        Diagnostics.Arrange("authorization", authorization ?? "(none)");
        Diagnostics.Arrange("range", range ?? "(none)");
        byte[] head = RtspRequestFormatter.Format(RtspMethod.Options, "*", 1, null, range, options, authorization);
        Diagnostics.ActSent(head);
        return Encoding.Latin1.GetString(head);
    }
}
