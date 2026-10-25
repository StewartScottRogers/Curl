using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;

namespace Curl.Engine;

[TestClass]
public sealed class TransferHeaderStoreTests
{
    private const int Last = ITransferHeaders.LastRequest;

    private static TransferHeaderStore StoreOf(params string[] lines)
    {
        TransferHeaderStore store = new();
        foreach (string line in lines)
        {
            store.RecordLine(line);
        }

        return store;
    }

    private static List<CurlHeader> Walk(TransferHeaderStore store, CurlHeaderOrigin origin, int request)
    {
        List<CurlHeader> walked = [];
        for (CurlHeader? header = store.Next(origin, request, null); header is not null; header = store.Next(origin, request, header))
        {
            walked.Add(header);
        }

        return walked;
    }

    [TestMethod]
    public void Find_OneHeader_AnswersItWithItsAmountIndexAndOrigin()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Content-Type:  text/plain \t", string.Empty);

        CurlHeaderCode code = store.Find("content-TYPE", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? header);

        Assert.AreEqual(CurlHeaderCode.Ok, code);
        Assert.AreEqual(new CurlHeader("Content-Type", "text/plain", 1, 0, CurlHeaderOrigin.Header), header);
    }

    [TestMethod]
    public void Find_RepeatedHeader_AnswersEachByIndexAndBadIndexPastThem()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Set-Cookie: a=1", "Server: s", "set-cookie: b=2", string.Empty);

        store.Find("Set-Cookie", 1, CurlHeaderOrigin.Header, 0, out CurlHeader? second);
        CurlHeaderCode past = store.Find("Set-Cookie", 2, CurlHeaderOrigin.Header, 0, out CurlHeader? none);

        Assert.AreEqual(new CurlHeader("set-cookie", "b=2", 2, 1, CurlHeaderOrigin.Header), second);
        Assert.AreEqual(CurlHeaderCode.BadIndex, past);
        Assert.IsNull(none);
    }

    [TestMethod]
    public void Find_NameNotReceivedOrOriginNotAsked_AnswersMissing()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.Missing, store.Find("Date", 0, CurlHeaderOrigin.Header, Last, out _));
        Assert.AreEqual(CurlHeaderCode.Missing, store.Find("Server", 0, CurlHeaderOrigin.Trailer, Last, out _));
    }

    [TestMethod]
    public void Find_NothingRecorded_AnswersNoHeaders()
    {
        Assert.AreEqual(CurlHeaderCode.NoHeaders, new TransferHeaderStore().Find("Server", 0, CurlHeaderOrigin.Header, Last, out _));
    }

    [TestMethod]
    public void Find_RequestNotMade_AnswersNoRequest()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.NoRequest, store.Find("Server", 0, CurlHeaderOrigin.Header, 1, out _));
    }

    [TestMethod]
    [DataRow(-1, CurlHeaderOrigin.Header, Last)]
    [DataRow(0, CurlHeaderOrigin.None, Last)]
    [DataRow(0, (CurlHeaderOrigin)(1 << 5), Last)]
    [DataRow(0, CurlHeaderOrigin.Header, -2)]
    public void Find_ArgumentLibcurlRefuses_AnswersBadArgument(int index, CurlHeaderOrigin origin, int request)
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.BadArgument, store.Find("Server", index, origin, request, out CurlHeader? header));
        Assert.IsNull(header);
    }

    [TestMethod]
    public void RecordLine_TwoResponses_NumbersEachRequestAndLastRequestIsTheSecond()
    {
        TransferHeaderStore store = StoreOf(
            "HTTP/1.1 302 Found", "Location: /b", string.Empty,
            "HTTP/1.1 200 OK", "Server: second", string.Empty);

        store.Find("Location", 0, CurlHeaderOrigin.Header, 0, out CurlHeader? location);
        store.Find("Server", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? server);

        Assert.AreEqual("/b", location?.Value);
        Assert.AreEqual("second", server?.Value);
        Assert.AreEqual(CurlHeaderCode.Missing, store.Find("Location", 0, CurlHeaderOrigin.Header, 1, out _));
    }

    [TestMethod]
    public void RecordLine_InformationalConnectAndTrailers_GiveEachItsOrigin()
    {
        TransferHeaderStore store = new();
        store.MarkConnectReplyHead();
        foreach (string line in new[]
        {
            "HTTP/1.1 200 Connection established", "Via: proxy", string.Empty,
            "HTTP/1.1 103 Early Hints", "Link: </a>", string.Empty,
            "HTTP/1.1 200 OK", "Server: s", string.Empty,
            "Checksum: 1", string.Empty,
        })
        {
            store.RecordLine(line);
        }

        List<CurlHeader> all = Walk(store, (CurlHeaderOrigin)31, 0);

        CollectionAssert.AreEqual(
            new[] { CurlHeaderOrigin.Connect, CurlHeaderOrigin.Informational, CurlHeaderOrigin.Header, CurlHeaderOrigin.Trailer },
            all.Select(header => header.Origin).ToArray());
    }

    [TestMethod]
    public void RecordLine_Http2StatusLine_RecordsTheStatusAsAPseudoHeader()
    {
        TransferHeaderStore store = StoreOf("HTTP/2 404", "server: s", string.Empty);

        store.Find(":status", 0, CurlHeaderOrigin.Pseudo, Last, out CurlHeader? status);

        Assert.AreEqual(new CurlHeader(":status", "404", 1, 0, CurlHeaderOrigin.Pseudo), status);
        Assert.AreEqual(CurlHeaderCode.Missing, store.Find(":status", 0, CurlHeaderOrigin.Header, Last, out _));
    }

    [TestMethod]
    public void RecordLine_StatusLineWithoutACode_RecordsItsHeadersAsPlainHeaders()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.Ok, store.Find("Server", 0, CurlHeaderOrigin.Header, Last, out _));
    }

    [TestMethod]
    public void RecordLine_FoldedLines_AppendToTheHeaderJoinedByOneSpaceWithoutTheirBlanks()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "X-Long: one", "  \ttwo  ", "\tthree", "   ", string.Empty);

        store.Find("X-Long", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? header);

        Assert.AreEqual("one two three", header?.Value);
    }

    [TestMethod]
    public void RecordLine_FoldWithLeadingTabs_UnfoldsAsUpstreamTest1941()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Server: from ", " the\t", "\t\tconnect", string.Empty);

        store.Find("Server", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? header);

        Assert.AreEqual("from the connect", header?.Value);
    }

    [TestMethod]
    public void RecordLine_FoldAfterAnEmptyValue_BecomesTheValueWithoutALeadingSpace()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Content-Type:", " text/html", string.Empty);

        store.Find("Content-Type", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? header);

        Assert.AreEqual("text/html", header?.Value);
    }

    [TestMethod]
    public void RecordLine_FoldBeforeAnyHeaderOrLineWithoutAColon_IsDropped()
    {
        TransferHeaderStore store = StoreOf(" stray", "HTTP/1.1 200 OK", "no colon here", string.Empty);

        Assert.AreEqual(CurlHeaderCode.NoHeaders, store.Find("no colon here", 0, CurlHeaderOrigin.Header, Last, out _));
    }

    [TestMethod]
    public void Next_WalksTheRequestsHeadersOfTheOriginInOrderWithEachNamesIndex()
    {
        TransferHeaderStore store = StoreOf(
            "HTTP/1.1 100 Continue", "X-Early: e", string.Empty,
            "HTTP/1.1 200 OK", "A: 1", "B: 2", "a: 3", string.Empty);

        List<CurlHeader> walked = Walk(store, CurlHeaderOrigin.Header, Last);

        CollectionAssert.AreEqual(
            new[]
            {
                new CurlHeader("A", "1", 2, 0, CurlHeaderOrigin.Header),
                new CurlHeader("B", "2", 1, 0, CurlHeaderOrigin.Header),
                new CurlHeader("a", "3", 2, 1, CurlHeaderOrigin.Header),
            },
            walked);
    }

    [TestMethod]
    public void Next_RequestNotMade_AnswersNull()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "A: 1", string.Empty);

        Assert.IsNull(store.Next(CurlHeaderOrigin.Header, 1, null));
    }

    [TestMethod]
    public void Next_PreviousTheStoreDidNotAnswer_AnswersNull()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "A: 1", "B: 2", string.Empty);

        Assert.IsNull(store.Next(CurlHeaderOrigin.Header, Last, new CurlHeader("A", "1", 1, 0, CurlHeaderOrigin.Header)));
    }

    [TestMethod]
    public void Next_PreviousFromBeforeAClear_AnswersNull()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "A: 1", "B: 2", string.Empty);
        CurlHeader? first = store.Next(CurlHeaderOrigin.Header, Last, null);
        store.Clear();
        store.RecordLine("HTTP/1.1 200 OK");
        store.RecordLine("A: 1");

        Assert.IsNotNull(first);
        Assert.IsNull(store.Next(CurlHeaderOrigin.Header, Last, first));
    }

    [TestMethod]
    public void Clear_ForgetsEveryHeaderAndTheRequestCount()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 301 Moved", "A: 1", string.Empty, "HTTP/1.1 200 OK", "B: 2", string.Empty);
        store.MarkConnectReplyHead();

        store.Clear();
        store.RecordLine("HTTP/1.1 200 OK");
        store.RecordLine("C: 3");

        Assert.AreEqual(CurlHeaderCode.NoRequest, store.Find("C", 0, CurlHeaderOrigin.Header, 1, out _));
        Assert.AreEqual(CurlHeaderCode.Ok, store.Find("C", 0, CurlHeaderOrigin.Header, 0, out _));
        Assert.AreEqual(CurlHeaderCode.Missing, store.Find("A", 0, CurlHeaderOrigin.Header | CurlHeaderOrigin.Connect, 0, out _));
    }

    [TestMethod]
    [DataRow("HTTP/1.0 200 OK", 1L)]
    [DataRow("HTTP/1.1 200 OK", 2L)]
    [DataRow("HTTP/2 200", 3L)]
    [DataRow("HTTP/3 200", 30L)]
    [DataRow("HTTP/4 200", 0L)]
    public void HttpVersion_StatusLine_IsItsCurlHttpVersionNumber(string statusLine, long expected)
    {
        TransferHeaderStore store = StoreOf(statusLine);

        Assert.AreEqual(expected, store.HttpVersion);
    }

    [TestMethod]
    public void HttpVersion_ConnectReplyHeadThenClear_StaysZero()
    {
        TransferHeaderStore store = new();
        store.MarkConnectReplyHead();
        store.RecordLine("HTTP/1.0 200 Connection established");
        Assert.AreEqual(0L, store.HttpVersion);

        store.RecordLine(string.Empty);
        store.RecordLine("HTTP/1.1 200 OK");
        store.Clear();

        Assert.AreEqual(0L, store.HttpVersion);
    }

    [TestMethod]
    public void ResponseCodeAndContentLength_FinalHead_AreItsStatusAndLength()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 100 Continue", "Content-Length: 99", string.Empty, "HTTP/1.1 200 OK", "Content-Length: 7");

        Assert.AreEqual(200L, store.ResponseCode);
        Assert.AreEqual(7L, store.ContentLength);
    }

    [TestMethod]
    public void ResponseCodeAndContentLength_InformationalHead_IsItsStatusAndNoLength()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 100 Continue", "Content-Length: 99");

        Assert.AreEqual(100L, store.ResponseCode);
        Assert.AreEqual(-1L, store.ContentLength);
    }

    [TestMethod]
    public void ResponseCodeAndContentLength_ConnectReplyHead_LeavesBothUnset()
    {
        TransferHeaderStore store = new();
        store.MarkConnectReplyHead();
        store.RecordLine("HTTP/1.1 200 Connection established");
        store.RecordLine("Content-Length: 3");

        Assert.AreEqual(0L, store.ResponseCode);
        Assert.AreEqual(-1L, store.ContentLength);
    }

    [TestMethod]
    public void ResponseCodeAndContentLength_UnreadableStatusAndLength_AreZeroAndMinusOne()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 2x0 OK", "Content-Length: -4");

        Assert.AreEqual(0L, store.ResponseCode);
        Assert.AreEqual(-1L, store.ContentLength);
    }

    [TestMethod]
    public void ResponseHeadRead_StatusLineAndContentLength_IsCalledForEachAndClearResetsBoth()
    {
        List<(long Code, long Length)> seen = [];
        TransferHeaderStore store = new();
        store.ResponseHeadRead = () => seen.Add((store.ResponseCode, store.ContentLength));

        store.RecordLine("HTTP/1.1 200 OK");
        store.RecordLine("Server: x");
        store.RecordLine("content-length: 7");
        store.Clear();

        CollectionAssert.AreEqual(new List<(long, long)> { (200, -1), (200, 7) }, seen);
        Assert.AreEqual(0L, store.ResponseCode);
        Assert.AreEqual(-1L, store.ContentLength);
    }
}
