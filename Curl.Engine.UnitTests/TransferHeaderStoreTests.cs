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
    public void TryGet_OneHeader_AnswersItWithItsAmountIndexAndOrigin()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Content-Type:  text/plain \t", string.Empty);

        CurlHeaderCode code = store.TryGet("content-TYPE", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? header);

        Assert.AreEqual(CurlHeaderCode.Ok, code);
        Assert.AreEqual(new CurlHeader("Content-Type", "text/plain", 1, 0, CurlHeaderOrigin.Header), header);
    }

    [TestMethod]
    public void TryGet_RepeatedHeader_AnswersEachByIndexAndBadIndexPastThem()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Set-Cookie: a=1", "Server: s", "set-cookie: b=2", string.Empty);

        store.TryGet("Set-Cookie", 1, CurlHeaderOrigin.Header, 0, out CurlHeader? second);
        CurlHeaderCode past = store.TryGet("Set-Cookie", 2, CurlHeaderOrigin.Header, 0, out CurlHeader? none);

        Assert.AreEqual(new CurlHeader("set-cookie", "b=2", 2, 1, CurlHeaderOrigin.Header), second);
        Assert.AreEqual(CurlHeaderCode.BadIndex, past);
        Assert.IsNull(none);
    }

    [TestMethod]
    public void TryGet_NameNotReceivedOrOriginNotAsked_AnswersMissing()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.Missing, store.TryGet("Date", 0, CurlHeaderOrigin.Header, Last, out _));
        Assert.AreEqual(CurlHeaderCode.Missing, store.TryGet("Server", 0, CurlHeaderOrigin.Trailer, Last, out _));
    }

    [TestMethod]
    public void TryGet_NothingRecorded_AnswersNoHeaders()
    {
        Assert.AreEqual(CurlHeaderCode.NoHeaders, new TransferHeaderStore().TryGet("Server", 0, CurlHeaderOrigin.Header, Last, out _));
    }

    [TestMethod]
    public void TryGet_RequestNotMade_AnswersNoRequest()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.NoRequest, store.TryGet("Server", 0, CurlHeaderOrigin.Header, 1, out _));
    }

    [TestMethod]
    [DataRow(-1, CurlHeaderOrigin.Header, Last)]
    [DataRow(0, CurlHeaderOrigin.None, Last)]
    [DataRow(0, (CurlHeaderOrigin)(1 << 5), Last)]
    [DataRow(0, CurlHeaderOrigin.Header, -2)]
    public void TryGet_ArgumentLibcurlRefuses_AnswersBadArgument(int index, CurlHeaderOrigin origin, int request)
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.BadArgument, store.TryGet("Server", index, origin, request, out CurlHeader? header));
        Assert.IsNull(header);
    }

    [TestMethod]
    public void RecordLine_TwoResponses_NumbersEachRequestAndLastRequestIsTheSecond()
    {
        TransferHeaderStore store = StoreOf(
            "HTTP/1.1 302 Found", "Location: /b", string.Empty,
            "HTTP/1.1 200 OK", "Server: second", string.Empty);

        store.TryGet("Location", 0, CurlHeaderOrigin.Header, 0, out CurlHeader? location);
        store.TryGet("Server", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? server);

        Assert.AreEqual("/b", location?.Value);
        Assert.AreEqual("second", server?.Value);
        Assert.AreEqual(CurlHeaderCode.Missing, store.TryGet("Location", 0, CurlHeaderOrigin.Header, 1, out _));
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

        store.TryGet(":status", 0, CurlHeaderOrigin.Pseudo, Last, out CurlHeader? status);

        Assert.AreEqual(new CurlHeader(":status", "404", 1, 0, CurlHeaderOrigin.Pseudo), status);
        Assert.AreEqual(CurlHeaderCode.Missing, store.TryGet(":status", 0, CurlHeaderOrigin.Header, Last, out _));
    }

    [TestMethod]
    public void RecordLine_StatusLineWithoutACode_RecordsItsHeadersAsPlainHeaders()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1", "Server: s", string.Empty);

        Assert.AreEqual(CurlHeaderCode.Ok, store.TryGet("Server", 0, CurlHeaderOrigin.Header, Last, out _));
    }

    [TestMethod]
    public void RecordLine_FoldedLines_AppendToTheHeaderKeepingOneLeadingBlank()
    {
        TransferHeaderStore store = StoreOf("HTTP/1.1 200 OK", "X-Long: one", "  \ttwo  ", "\tthree", "   ", string.Empty);

        store.TryGet("X-Long", 0, CurlHeaderOrigin.Header, Last, out CurlHeader? header);

        Assert.AreEqual("one\ttwo\tthree", header?.Value);
    }

    [TestMethod]
    public void RecordLine_FoldBeforeAnyHeaderOrLineWithoutAColon_IsDropped()
    {
        TransferHeaderStore store = StoreOf(" stray", "HTTP/1.1 200 OK", "no colon here", string.Empty);

        Assert.AreEqual(CurlHeaderCode.NoHeaders, store.TryGet("no colon here", 0, CurlHeaderOrigin.Header, Last, out _));
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

        Assert.AreEqual(CurlHeaderCode.NoRequest, store.TryGet("C", 0, CurlHeaderOrigin.Header, 1, out _));
        Assert.AreEqual(CurlHeaderCode.Ok, store.TryGet("C", 0, CurlHeaderOrigin.Header, 0, out _));
        Assert.AreEqual(CurlHeaderCode.Missing, store.TryGet("A", 0, CurlHeaderOrigin.Header | CurlHeaderOrigin.Connect, 0, out _));
    }
}
