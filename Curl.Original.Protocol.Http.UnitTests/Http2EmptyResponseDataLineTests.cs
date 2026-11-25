using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins the empty data event, <c>-v</c>'s <c>{ [0 bytes data]</c>, that curl 8.18.0 with nghttp2
/// reports for an HTTP/2 response with no body, or one a trailer ends (measured, BL-5946).
/// </summary>
[TestClass]
public sealed class Http2EmptyResponseDataLineTests
{
    private const string EmptyData = "{ ";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("200")]
    [DataRow("204")]
    public async Task ExecuteAsync_HeadRequestWhoseHeadEndsTheStream_ReportsOneEmptyDataEvent(string status)
    {
        List<string> events = await EventsOfAsync(noBody: true, Headers(isEndStream: true, new(":status", status), new("content-length", "3")));

        CollectionAssert.AreEqual(new[] { EmptyData }, DataEventsOf(events));
        Assert.AreEqual("< \r\n", events[events.IndexOf(EmptyData) - 1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_HeadRequestATrailerEnds_ReportsOneEmptyDataEventAfterTheTrailer()
    {
        List<string> events = await EventsOfAsync(
            noBody: true,
            Headers(isEndStream: false, new(":status", "200"), new("content-length", "3")),
            Headers(isEndStream: true, new HeaderField("x-t", "v")));

        CollectionAssert.AreEqual(new[] { EmptyData }, DataEventsOf(events));
        Assert.AreEqual("< x-t: v\r\n", events[events.IndexOf(EmptyData) - 1]);
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthZeroEndingOnHeaders_ReportsOneEmptyDataEvent()
    {
        List<string> events = await EventsOfAsync(noBody: false, Headers(isEndStream: true, new(":status", "200"), new("content-length", "0")));

        CollectionAssert.AreEqual(new[] { EmptyData }, DataEventsOf(events));
    }

    [TestMethod]
    public async Task ExecuteAsync_ContentLengthZeroEndingOnEmptyData_ReportsOneEmptyDataEvent()
    {
        List<string> events = await EventsOfAsync(
            noBody: false,
            Headers(isEndStream: false, new(":status", "200"), new("content-length", "0")),
            Data(string.Empty, isEndStream: true));

        CollectionAssert.AreEqual(new[] { EmptyData }, DataEventsOf(events));
    }

    [TestMethod]
    [DataRow("204")]
    [DataRow("304")]
    [DataRow("200")]
    public async Task ExecuteAsync_GetWhoseHeadWithoutALengthEndsTheStream_ReportsOneEmptyDataEvent(string status)
    {
        List<string> events = await EventsOfAsync(noBody: false, Headers(isEndStream: true, new HeaderField(":status", status)));

        CollectionAssert.AreEqual(new[] { EmptyData }, DataEventsOf(events));
    }

    [TestMethod]
    public async Task ExecuteAsync_BodyATrailerEnds_ReportsEmptyDataAfterTheTrailer()
    {
        List<string> events = await EventsOfAsync(
            noBody: false,
            Headers(isEndStream: false, new(":status", "200"), new("content-length", "3")),
            Data("abc", isEndStream: false),
            Headers(isEndStream: true, new HeaderField("x-t", "v")));

        CollectionAssert.AreEqual(new[] { "{ abc", EmptyData }, DataEventsOf(events));
        Assert.AreEqual("< x-t: v\r\n", events[events.IndexOf(EmptyData) - 1]);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExecuteAsync_BodyEndedByData_ReportsNoEmptyData(bool endsOnAnEmptyFrame)
    {
        Http2Frame[] data = endsOnAnEmptyFrame
            ? [Data("abc", isEndStream: false), Data(string.Empty, isEndStream: true)]
            : [Data("abc", isEndStream: true)];

        List<string> events = await EventsOfAsync(
            noBody: false,
            [Headers(isEndStream: false, new(":status", "200"), new("content-length", "3")), .. data]);

        CollectionAssert.AreEqual(new[] { "{ abc" }, DataEventsOf(events));
    }

    [TestMethod]
    public async Task ExecuteAsync_FollowedRedirectWhoseBodyIsIgnored_ReportsNoData()
    {
        List<string> events = await EventsOfAsync(
            noBody: false,
            followRedirects: true,
            Headers(isEndStream: true, new(":status", "301"), new("location", "/y"), new("content-length", "0")));

        CollectionAssert.AreEqual(Array.Empty<string>(), DataEventsOf(events));
    }

    private static Http2Frame Headers(bool isEndStream, params HeaderField[] fields) =>
        Http2FrameFactory.CreateHeaders(1, new HpackEncoder().Encode(fields), isEndStream, isEndHeaders: true);

    private static Http2Frame Data(string text, bool isEndStream) =>
        Http2FrameFactory.CreateData(1, System.Text.Encoding.ASCII.GetBytes(text), isEndStream);

    private static string[] DataEventsOf(List<string> events) =>
        [.. events.Where(line => line.StartsWith(EmptyData, StringComparison.Ordinal))];

    private Task<List<string>> EventsOfAsync(bool noBody, params Http2Frame[] frames) =>
        EventsOfAsync(noBody, followRedirects: false, frames);

    private async Task<List<string>> EventsOfAsync(bool noBody, bool followRedirects, params Http2Frame[] frames)
    {
        byte[] served =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettingsAcknowledgement()),
            .. frames.SelectMany(Http2FrameCodec.Serialize),
        ];
        QueueConnector connector = QueueConnector.For(new ScriptedConnection(served, 65536));
        RecordingTransferEvents events = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://127.0.0.1:21612/x"),
            Output = new MemoryStream(),
            Events = events,
            NoBody = noBody,
            Http = new HttpRequestOptions { Version = HttpVersionPreference.Http2PriorKnowledge, FollowRedirects = followRedirects },
        };
        Diagnostics.Arrange("-I, -L, response frames", $"{noBody}, {followRedirects}, {frames.Length}");

        TransferResult result = await new HttpProtocolHandler(connector, new SilentAuthenticator()).ExecuteAsync(context);

        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("events", string.Join(" | ", events.Events));
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        return events.Events;
    }
}
