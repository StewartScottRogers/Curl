using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins server push through <see cref="Http2Session" /> (BL-2516, curl 8.22.0's
/// <c>lib/http2.c</c> <c>push_promise</c>): ENABLE_PUSH in the preface, a PUSH_PROMISE offered to
/// the stream's <see cref="IHttp2PushSink" /> with curl's <c>name:value</c> header lines, the
/// pushed response read through what the sink was given, and a refused push reset with CANCEL.
/// </summary>
[TestClass]
public sealed class Http2SessionPushTests
{
    /// <summary>The preface curl sends with a push callback: the measured one with ENABLE_PUSH 1.</summary>
    private const string PushEnabledPreface =
        "505249202a20485454502f322e300d0a0d0a534d0d0a0d0a"
        + "000012040000000000" + "000300000064" + "000400010000" + "000200000001"
        + "000004080000000000" + "3e7f0001";

    /// <summary>The measured preface curl sends with no push callback, ENABLE_PUSH 0.</summary>
    private const string PushDisabledPreface =
        "505249202a20485454502f322e300d0a0d0a534d0d0a0d0a"
        + "000012040000000000" + "000300000064" + "000400010000" + "000200000000"
        + "000004080000000000" + "3e7f0001";

    private static readonly HeaderField[] PromisedRequest =
    [
        new(":method", "GET"),
        new(":scheme", "https"),
        new(":authority", "example.com"),
        new(":path", "/style.css"),
        new("x-push", "yes"),
    ];

    private static readonly string[] PromisedRequestLines =
        [":method:GET", ":scheme:https", ":authority:example.com", ":path:/style.css", "x-push:yes"];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task StartStreamAsync_PushEnabled_SendsThePrefaceWithEnablePushOne()
    {
        ScriptedConnection connection = new([], 65536);
        Http2Session session = new(connection, isPushEnabled: true);
        Diagnostics.Arrange("session", "push enabled");

        _ = await StartAsync(session, "/", sink: null);

        byte[] preface = connection.Written[..Convert.FromHexString(PushEnabledPreface).Length];
        Diagnostics.Bytes("preface written", preface);
        Diagnostics.Diff("preface", Convert.FromHexString(PushEnabledPreface), preface);
        CollectionAssert.AreEqual(Convert.FromHexString(PushEnabledPreface), preface);
    }

    [TestMethod]
    public async Task StartStreamAsync_PushNotEnabled_SendsTheMeasuredPrefaceWithEnablePushZero()
    {
        ScriptedConnection connection = new([], 65536);
        Http2Session session = new(connection);
        Diagnostics.Arrange("session", "push not enabled (the default)");

        _ = await StartAsync(session, "/", sink: null);

        byte[] preface = connection.Written[..Convert.FromHexString(PushDisabledPreface).Length];
        Diagnostics.Bytes("preface written", preface);
        Diagnostics.Diff("preface", Convert.FromHexString(PushDisabledPreface), preface);
        CollectionAssert.AreEqual(Convert.FromHexString(PushDisabledPreface), preface);
    }

    [TestMethod]
    public async Task ReadAsync_PushPromiseTheSinkTakes_HandsItTheHeaderLinesAndThePushedResponse()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            Http2FrameFactory.CreatePushPromise(1, 2, server.Encode(PromisedRequest), isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("x-push", "yes")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(2, server.Encode([new(":status", "200"), new("content-type", "text/css")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(2, "pushed"u8.ToArray(), isEndStream: true),
            Http2FrameFactory.CreateData(1, "parent"u8.ToArray(), isEndStream: true));
        Diagnostics.Arrange("server frames", "SETTINGS, PUSH_PROMISE 1 promising 2, HEADERS 1, HEADERS 2, DATA 2 pushed end, DATA 1 parent end");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection, isPushEnabled: true);
        RecordingPushSink sink = new(accepts: true);
        Http2StreamConnection parent = await StartAsync(session, "/", sink);

        string parentResponse = await ReadToEndAsync(parent.ReadAsync);
        IHttpPushedResponse pushed = sink.Responses.Single();
        string pushedResponse = await ReadToEndAsync((buffer, token) => pushed.Content.ReadAsync(buffer, token));

        Diagnostics.Act("header lines offered", string.Join(" | ", sink.HeaderLines.Single()));
        Diagnostics.Act("parent response", Visible(parentResponse));
        Diagnostics.Act("pushed response", Visible(pushedResponse));
        Diagnostics.Assert("pushed response", Visible("HTTP/2 200 \r\ncontent-type: text/css\r\n\r\npushed"), Visible(pushedResponse));
        CollectionAssert.AreEqual(PromisedRequestLines, sink.HeaderLines.Single().ToArray());
        Assert.AreEqual(2, pushed.StreamId);
        Assert.AreEqual("HTTP/2 200 \r\ncontent-type: text/css\r\n\r\npushed", pushedResponse);
        Assert.AreEqual("HTTP/2 200 \r\nx-push: yes\r\n\r\nparent", parentResponse);
        Assert.IsFalse((await SentFrames(connection)).Any(frame => frame.Type == Http2FrameType.RstStream), "a taken push is not reset");
    }

    [TestMethod]
    public async Task ReadAsync_PushPromiseTheSinkRefuses_ResetsItWithCancelAndReadsTheParentWhole()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            Http2FrameFactory.CreatePushPromise(1, 2, server.Encode(PromisedRequest), isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "200"), new("x-push", "yes")]), isEndStream: false, isEndHeaders: true),
            Http2FrameFactory.CreateData(1, "parent"u8.ToArray(), isEndStream: true));
        Diagnostics.Arrange("server frames", "SETTINGS, PUSH_PROMISE 1 promising 2, HEADERS 1 reusing the promise's x-push entry, DATA 1 parent end");
        Diagnostics.Bytes("scripted server bytes", scripted);
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection, isPushEnabled: true);
        RecordingPushSink sink = new(accepts: false);
        Http2StreamConnection parent = await StartAsync(session, "/", sink);

        string parentResponse = await ReadToEndAsync(parent.ReadAsync);

        Http2Frame reset = (await SentFrames(connection)).Single(frame => frame.Type == Http2FrameType.RstStream);
        Diagnostics.Act("parent response", Visible(parentResponse));
        Diagnostics.Act("reset sent", $"stream {reset.StreamId}, payload {Convert.ToHexString(reset.Payload.Span)}");
        Diagnostics.Assert("reset", "stream 2, payload 00000008", $"stream {reset.StreamId}, payload {Convert.ToHexString(reset.Payload.Span)}");
        CollectionAssert.AreEqual(PromisedRequestLines, sink.HeaderLines.Single().ToArray());
        Assert.AreEqual(2, reset.StreamId);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 8 }, reset.Payload.ToArray(), "RST_STREAM CANCEL");
        Assert.AreEqual("HTTP/2 200 \r\nx-push: yes\r\n\r\nparent", parentResponse);
    }

    [TestMethod]
    public async Task ReadAsync_PushPromiseOnAStreamWithNoSink_ResetsItWithCancel()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(
            Http2FrameFactory.CreatePushPromise(1, 2, server.Encode(PromisedRequest), isEndHeaders: true),
            Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", "204")]), isEndStream: true, isEndHeaders: true));
        Diagnostics.Arrange("server frames", "SETTINGS, PUSH_PROMISE 1 promising 2, HEADERS 1 204 end");
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection, isPushEnabled: true);
        Http2StreamConnection parent = await StartAsync(session, "/", sink: null);

        string parentResponse = await ReadToEndAsync(parent.ReadAsync);

        Http2Frame reset = (await SentFrames(connection)).Single(frame => frame.Type == Http2FrameType.RstStream);
        Diagnostics.Act("reset sent", $"stream {reset.StreamId}, payload {Convert.ToHexString(reset.Payload.Span)}");
        Diagnostics.Assert("reset stream", 2, reset.StreamId);
        Assert.AreEqual(2, reset.StreamId);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 8 }, reset.Payload.ToArray(), "RST_STREAM CANCEL");
        Assert.AreEqual("HTTP/2 204 \r\n\r\n", parentResponse);
    }

    [TestMethod]
    public async Task ReceiveAsync_PushPromiseOnAStreamNoTransferOwns_ResetsItWithCancel()
    {
        HpackEncoder server = new();
        byte[] scripted = Frames(Http2FrameFactory.CreatePushPromise(3, 2, server.Encode(PromisedRequest), isEndHeaders: true));
        Diagnostics.Arrange("server frames", "SETTINGS, PUSH_PROMISE 3 promising 2, stream 3 open in the frame layer but owned by no transfer");
        ScriptedConnection connection = new(scripted, 65536);
        Http2Session session = new(connection, isPushEnabled: true);
        _ = await StartAsync(session, "/", new RecordingPushSink(accepts: true));
        await session.Frames.WriteHeadersAsync(session.Frames.OpenStream(), server.Encode([new(":method", "GET")]), isEndStream: true, CancellationToken.None);

        await session.ReceiveAsync(null, CancellationToken.None);
        await session.ReceiveAsync(null, CancellationToken.None);

        Http2Frame reset = (await SentFrames(connection)).Single(frame => frame.Type == Http2FrameType.RstStream);
        Diagnostics.Act("reset sent", $"stream {reset.StreamId}, payload {Convert.ToHexString(reset.Payload.Span)}");
        Diagnostics.Assert("reset stream", 2, reset.StreamId);
        Assert.AreEqual(2, reset.StreamId);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 8 }, reset.Payload.ToArray(), "RST_STREAM CANCEL");
    }

    private static async Task<Http2StreamConnection> StartAsync(Http2Session session, string path, IHttp2PushSink? sink)
    {
        Http2StreamConnection stream = (Http2StreamConnection)session.CreateStream("https", 0, ignoresBody: false);
        stream.PushSink = sink;
        await stream.WriteAsync(Encoding.Latin1.GetBytes($"GET {path} HTTP/1.1\r\nHost: example.com\r\n\r\n"), CancellationToken.None);
        return stream;
    }

    private static async Task<string> ReadToEndAsync(Func<Memory<byte>, CancellationToken, ValueTask<int>> read)
    {
        MemoryStream response = new();
        byte[] buffer = new byte[256];
        int count;
        while ((count = await read(buffer, CancellationToken.None)) > 0)
        {
            response.Write(buffer, 0, count);
        }

        return Encoding.Latin1.GetString(response.ToArray());
    }

    /// <summary>Gives a server's empty SETTINGS followed by <paramref name="frames" />.</summary>
    private static byte[] Frames(params Http2Frame[] frames) =>
    [
        .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([])),
        .. frames.SelectMany(Http2FrameCodec.Serialize),
    ];

    /// <summary>Reads the frames the client wrote after its preface.</summary>
    private static async Task<List<Http2Frame>> SentFrames(ScriptedConnection connection)
    {
        byte[] written = connection.Written;
        using MemoryStream input = new(written, Http2Connection.ClientPreface.Length, written.Length - Http2Connection.ClientPreface.Length);
        List<Http2Frame> frames = [];
        while (await Http2FrameCodec.ReadAsync(input, Http2FrameCodec.LargestMaximumFrameSize, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static string Visible(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");

    /// <summary>Takes or refuses every push, recording what it was offered.</summary>
    private sealed class RecordingPushSink(bool accepts) : IHttp2PushSink
    {
        public List<IReadOnlyList<string>> HeaderLines { get; } = [];

        public List<IHttpPushedResponse> Responses { get; } = [];

        public bool TryAcceptPush(IReadOnlyList<string> headers, IHttpPushedResponse response)
        {
            HeaderLines.Add(headers);
            Responses.Add(response);
            return accepts;
        }
    }
}
