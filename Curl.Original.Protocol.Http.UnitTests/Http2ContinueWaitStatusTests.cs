using System.Net;
using System.Text;
using Curl.Http2;
using Curl.Original.Protocol.Http.Fakes;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-5970: the status of an HTTP/2 response that arrives during the wait for
/// <c>100 Continue</c> decides what happens to the body, as curl's nghttp2 build does (measured
/// with 8.18.0, <c>--http2-prior-knowledge -v -H "Expect: 100-continue" -d x=1</c>): a 100 sends
/// it at once, a 200 lets the wait run out and then sends it, and a 500 or a followed redirect
/// leaves it unsent. A stopped upload resets its stream with STREAM_CLOSED, and a 200 that ends
/// the stream before the held body goes out fails the send with exit 55 (BL-5972). A 500 that
/// arrives after the wait ran out, with the body waiting for window, stops it part way, resets
/// the stream once the response is read and leaves the connection intact (BL-5973). <c>-f</c> on
/// a status that ended the stream with the body unsent resets it too (BL-5975). The clock is
/// a fake one, advanced only for a 200.
/// </summary>
[TestClass]
public sealed class Http2ContinueWaitStatusTests
{
    private const string StopSending = "* HTTP error before end of send, stop sending";

    private const string AbortUpload = "* abort upload";

    private const int SettingsFrameLength = 9;

    /// <summary>How the response to a stopped upload ends.</summary>
    public enum Answer500
    {
        /// <summary>The head ends the stream.</summary>
        EndsAtHead,

        /// <summary>Window opens, and DATA then ends the stream.</summary>
        EndsAfterWindow,

        /// <summary>DATA ends the stream with the window still shut.</summary>
        EndsBeforeWindow,
    }

    [TestMethod]
    [DataRow(Answer500.EndsAtHead, 65535, DisplayName = "the head ends the stream: nothing more goes")]
    [DataRow(Answer500.EndsAfterWindow, 65536, DisplayName = "window opens first: what was read goes")]
    [DataRow(Answer500.EndsBeforeWindow, 65535, DisplayName = "the response ends before window opens")]
    public async Task ExecuteAsync_A500AfterTheWaitRanOutWhileTheBodyWaitsForWindow_StopsTheUploadAndKeepsTheConnection(Answer500 answer, int expectedBodyBytes)
    {
        byte[] error = Response(Headers("500", endsStream: answer == Answer500.EndsAtHead))[SettingsFrameLength..];
        byte[] end = Response(Data("err\n"))[SettingsFrameLength..];
        byte[] window =
        [
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateWindowUpdate(0, 65535)),
            .. Http2FrameCodec.Serialize(Http2FrameFactory.CreateWindowUpdate(1, 65535)),
        ];
        // The answer waits for the initial window's 65535 body bytes; the end, in
        // EndsAfterWindow, for the one-byte DATA frame the opened window lets out.
        List<(Func<byte[], bool> After, byte[] Bytes)> stages =
        [
            (_ => true, Response()),
            (written => written.Length > 65535, error),
        ];
        if (answer == Answer500.EndsAfterWindow)
        {
            stages.Add((_ => true, window));
            stages.Add((written => written is [0, 0, 1, 0, 0, ..], end));
        }
        else if (answer == Answer500.EndsBeforeWindow)
        {
            stages.Add((_ => true, end));
        }

        StagedConnection connection = new(stages);
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);

        Task<TransferResult> transfer = StartAsync(connection, events, time, body: new byte[200000]);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Http2Frame[] data = [.. (await FramesSentAsync(connection.Written)).Where(frame => frame.Type == Http2FrameType.Data)];
        Assert.AreEqual(expectedBodyBytes, data.Sum(frame => frame.Payload.Length));
        Assert.IsFalse(data.Any(frame => (frame.Flags & 0x1) != 0));
        CollectionAssert.AreEqual(new[] { "1:5" }, await ResetsSentAsync(connection.Written));
        Assert.IsFalse(events.Events.Any(line => line.Contains("sent off", StringComparison.Ordinal)));
        AssertInOrder(
            events.Events,
            "* Done waiting for 100-continue",
            "< HTTP/2 500 \r\n",
            StopSending,
            "* abort upload after having sent 65536 bytes",
            "< \r\n",
            "* Connection #0 to host h:80 left intact");
    }

    [TestMethod]
    public async Task ExecuteAsync_A100DuringTheWait_PrintsItsHeadAndSendsTheBodyWithoutWaiting()
    {
        byte[] response = Response(Headers("100", endsStream: false), Headers("200", endsStream: true));

        (TransferResult result, int bodyBytesSent, RecordingTransferEvents events) = await TransferAsync(response);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(3, bodyBytesSent);
        CollectionAssert.DoesNotContain(events.Info, "Done waiting for 100-continue");
        CollectionAssert.DoesNotContain(events.Events, StopSending);
        AssertInOrder(events.Events, "< HTTP/2 100 \r\n", "* upload completely sent off: 3 bytes", "< HTTP/2 200 \r\n");
    }

    [TestMethod]
    public async Task ExecuteAsync_A200DuringTheWait_PrintsItsHeadThenSendsTheBodyOnceTheWaitRunsOut()
    {
        AnswersAfterTheBodyConnection connection = new(Response(Headers("200", endsStream: false)), Response(Data("ok\n"))[SettingsFrameLength..]);
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);

        Task<TransferResult> transfer = StartAsync(connection, events, time);
        await connection.RestAwaited;
        int sentBeforeTheWaitRanOut = await BodyBytesSentAsync(connection.Written);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(0, sentBeforeTheWaitRanOut);
        Assert.AreEqual(3, await BodyBytesSentAsync(connection.Written));
        CollectionAssert.DoesNotContain(events.Events, StopSending);
        AssertInOrder(events.Events, "< HTTP/2 200 \r\n", "< \r\n", "* Done waiting for 100-continue", "* upload completely sent off: 3 bytes");
    }

    [TestMethod]
    public async Task ExecuteAsync_A500DuringTheWait_LeavesTheBodyUnsentAndSaysSoBeforeTheEmptyLine()
    {
        byte[] response = Response(Headers("500", endsStream: false), Data("err\n"));

        (TransferResult result, int bodyBytesSent, RecordingTransferEvents events) = await TransferAsync(response);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(0, bodyBytesSent);
        CollectionAssert.DoesNotContain(events.Info, "Done waiting for 100-continue");
        Assert.IsFalse(events.Events.Any(line => line.Contains("sent off", StringComparison.Ordinal)));
        AssertInOrder(events.Events, "< HTTP/2 500 \r\n", StopSending, AbortUpload, "< \r\n", "* Connection #0 to host h:80 left intact");
    }

    [TestMethod]
    public async Task ExecuteAsync_AFollowedRedirectDuringTheWait_StopsTheUploadWithoutRewindingIt()
    {
        byte[] response = Response(Headers("301", endsStream: true, new HeaderField("location", "/y")));

        (_, int bodyBytesSent, RecordingTransferEvents events) = await TransferAsync(response, followsRedirects: true);

        Assert.AreEqual(0, bodyBytesSent);
        CollectionAssert.DoesNotContain(events.Info, "Need to rewind upload for next request");
        AssertInOrder(events.Events, "< HTTP/2 301 \r\n", StopSending, AbortUpload, "< \r\n");
    }

    [TestMethod]
    [DataRow(true, DisplayName = "the response ends at its head")]
    [DataRow(false, DisplayName = "the response ends with DATA")]
    public async Task ExecuteAsync_AStoppedUpload_ResetsItsStreamWithStreamClosedOnce(bool endsAtHead)
    {
        byte[] response = endsAtHead
            ? Response(Headers("500", endsStream: true))
            : Response(Headers("500", endsStream: false), Data("err\n"));

        (TransferResult result, byte[] written, _) = await ExchangeAsync(response);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "1:5" }, await ResetsSentAsync(written));
    }

    [TestMethod]
    public async Task ExecuteAsync_AFollowedRedirectThatEndsTheStream_ResetsItWithStreamClosedOnce()
    {
        byte[] response = Response(Headers("301", endsStream: true, new HeaderField("location", "/y")));

        (_, byte[] written, _) = await ExchangeAsync(response, followsRedirects: true);

        CollectionAssert.AreEqual(new[] { "1:5" }, await ResetsSentAsync(written));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "the head ends the stream")]
    [DataRow(false, DisplayName = "DATA ends the stream")]
    public async Task ExecuteAsync_A200ThatEndsTheStreamBeforeTheHeldBody_SendsItThenFailsWith55LeavingTheConnectionIntact(bool endsAtHead)
    {
        byte[] response = endsAtHead
            ? Response(Headers("200", endsStream: true))
            : Response(Headers("200", endsStream: false), Data("ok\n"));
        ScriptedConnection connection = new(response, 65536);
        RecordingTransferEvents events = new();
        FakeTimeProvider time = new(DateTimeOffset.UnixEpoch);

        Task<TransferResult> transfer = StartAsync(connection, events, time);
        time.Advance(HttpRequestOptions.DefaultContinueWait);
        TransferResult result = await transfer;

        Assert.AreEqual(CurlExitCode.SendError, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual("Failed sending data to the peer", result.ErrorMessage);
        Http2Frame data = (await FramesSentAsync(connection.Written)).Single(frame => frame.Type == Http2FrameType.Data);
        Assert.AreEqual("x=1", Encoding.Latin1.GetString(data.Payload.Span));
        Assert.AreEqual(0x01, (byte)data.Flags);
        Assert.IsEmpty(await ResetsSentAsync(connection.Written));
        Assert.IsFalse(events.Events.Any(line => line.Contains("sent off", StringComparison.Ordinal)));
        Assert.IsFalse(events.Events.Any(line => line.StartsWith("} ", StringComparison.Ordinal) || line == "{ "), string.Join('|', events.Events));
        AssertInOrder(events.Events, "< HTTP/2 200 \r\n", "< \r\n", "* Done waiting for 100-continue", "* stream 1 closed", "* Connection #0 to host h:80 left intact");
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOnAStatusThatEndsTheStreamBeforeTheHeldBody_ResetsItWithStreamClosedOnce()
    {
        ScriptedConnection connection = new(Response(Headers("500", endsStream: true)), 65536);

        TransferResult result = await StartAsync(connection, new RecordingTransferEvents(), new FakeTimeProvider(DateTimeOffset.UnixEpoch), fail: HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(0, await BodyBytesSentAsync(connection.Written));
        CollectionAssert.AreEqual(new[] { "1:5" }, await ResetsSentAsync(connection.Written));
        Http2FrameType[] closing = [.. (await FramesSentAsync(connection.Written)).Select(frame => frame.Type).TakeLast(2)];
        CollectionAssert.AreEqual(new[] { Http2FrameType.RstStream, Http2FrameType.GoAway }, closing);
    }

    [TestMethod]
    public async Task ExecuteAsync_FailOnAStatusDuringTheWait_ReportsTheConnectionLeftIntact()
    {
        ScriptedConnection connection = new(Response(Headers("500", endsStream: true)), 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await StartAsync(connection, events, new FakeTimeProvider(DateTimeOffset.UnixEpoch), fail: HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, result.ErrorMessage);
        AssertInOrder(events.Events, "< HTTP/2 500 \r\n", "* The requested URL returned error: 500", "< \r\n", "* Connection #0 to host h:80 left intact");
        CollectionAssert.DoesNotContain(events.Info, "closing connection #0");
    }

    [TestMethod]
    public async Task ExecuteAsync_TheResetOfAFailedUploadCannotBeWritten_StillFailsWith22()
    {
        FailsTheResetConnection connection = new(new ScriptedConnection(Response(Headers("500", endsStream: true)), 65536));

        TransferResult result = await StartAsync(connection, new RecordingTransferEvents(), new FakeTimeProvider(DateTimeOffset.UnixEpoch), fail: HttpFailMode.Fail);

        Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode, result.ErrorMessage);
        Assert.IsTrue(connection.RefusedTheReset);
    }

    private static async Task<List<Http2Frame>> FramesSentAsync(byte[] written)
    {
        const int PrefaceLength = 24;
        using MemoryStream stream = new(written, PrefaceLength, written.Length - PrefaceLength);
        List<Http2Frame> frames = [];
        while (await Http2FrameCodec.ReadAsync(stream, 16384, CancellationToken.None) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static async Task<string[]> ResetsSentAsync(byte[] written) =>
        [.. (await FramesSentAsync(written))
            .Where(frame => frame.Type == Http2FrameType.RstStream)
            .Select(frame => $"{frame.StreamId}:{frame.Payload.Span[3]}")];

    private static void AssertInOrder(List<string> events, params string[] expected)
    {
        int[] positions = [.. expected.Select(line => events.IndexOf(line))];
        string all = string.Join('|', events);

        Assert.IsFalse(positions.Contains(-1), all);
        CollectionAssert.AreEqual(positions.Order().ToArray(), positions, all);
    }

    private static Func<HpackEncoder, Http2Frame> Headers(string status, bool endsStream, params HeaderField[] fields) =>
        server => Http2FrameFactory.CreateHeaders(1, server.Encode([new(":status", status), .. fields]), isEndStream: endsStream, isEndHeaders: true);

    private static Func<HpackEncoder, Http2Frame> Data(string text) =>
        _ => Http2FrameFactory.CreateData(1, Encoding.Latin1.GetBytes(text), isEndStream: true);

    private static byte[] Response(params Func<HpackEncoder, Http2Frame>[] frames)
    {
        HpackEncoder server = new();
        List<byte> bytes = [.. Http2FrameCodec.Serialize(Http2FrameFactory.CreateSettings([]))];
        foreach (Func<HpackEncoder, Http2Frame> frame in frames)
        {
            bytes.AddRange(Http2FrameCodec.Serialize(frame(server)));
        }

        return [.. bytes];
    }

    private static async Task<(TransferResult Result, int BodyBytesSent, RecordingTransferEvents Events)> TransferAsync(
        byte[] response,
        bool followsRedirects = false)
    {
        (TransferResult result, byte[] written, RecordingTransferEvents events) = await ExchangeAsync(response, followsRedirects);

        return (result, await BodyBytesSentAsync(written), events);
    }

    private static async Task<(TransferResult Result, byte[] Written, RecordingTransferEvents Events)> ExchangeAsync(
        byte[] response,
        bool followsRedirects = false)
    {
        ScriptedConnection connection = new(response, 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await StartAsync(connection, events, new FakeTimeProvider(DateTimeOffset.UnixEpoch), followsRedirects);

        return (result, connection.Written, events);
    }

    private static Task<TransferResult> StartAsync(
        IConnection connection,
        RecordingTransferEvents events,
        FakeTimeProvider time,
        bool followsRedirects = false,
        HttpFailMode fail = HttpFailMode.None,
        byte[]? body = null)
    {
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("http://h/x"),
            Output = new MemoryStream(),
            Events = events,
            TimeProvider = time,
            Http = new HttpRequestOptions
            {
                Version = HttpVersionPreference.Http2PriorKnowledge,
                FollowRedirects = followsRedirects,
                Fail = fail,
                Headers = ["Expect: 100-continue"],
                Body = new BytesBody(body ?? "x=1"u8.ToArray(), "application/x-www-form-urlencoded"),
            },
        };

        return new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(context).AsTask();
    }

    private static async Task<int> BodyBytesSentAsync(byte[] written) =>
        (await FramesSentAsync(written)).Where(frame => frame.Type == Http2FrameType.Data).Sum(frame => frame.Payload.Length);

    /// <summary>
    /// A server that gives each stage's bytes in turn, each once the stage's condition holds of
    /// the client's latest write or, for a total, of everything written.
    /// </summary>
    private sealed class StagedConnection(IReadOnlyList<(Func<byte[], bool> After, byte[] Bytes)> stages) : IConnection
    {
        private readonly MemoryStream written = new();

        private TaskCompletionSource wrote = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private byte[] latest = [];

        private int stage;

        private int offset;

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public byte[] Written => written.ToArray();

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (stage == stages.Count)
            {
                return 0;
            }

            (Func<byte[], bool> after, byte[] bytes) = stages[stage];
            while (offset == 0 && !after(latest) && !after(Written))
            {
                await wrote.Task.WaitAsync(cancellationToken);
                wrote = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            int read = Math.Min(buffer.Length, bytes.Length - offset);
            bytes.AsMemory(offset, read).CopyTo(buffer);
            offset += read;
            if (offset == bytes.Length)
            {
                stage++;
                offset = 0;
            }

            return read;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            written.Write(buffer.Span);
            latest = buffer.ToArray();
            wrote.TrySetResult();
            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary><paramref name="inner" />, except that it fails the write of an RST_STREAM frame.</summary>
    private sealed class FailsTheResetConnection(IConnection inner) : IConnection
    {
        public bool IsSecure => inner.IsSecure;

        public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

        public bool RefusedTheReset { get; private set; }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (buffer.Length == 13 && buffer.Span[3] == (byte)Http2FrameType.RstStream)
            {
                RefusedTheReset = true;
                throw new IOException("The connection is gone.");
            }

            return inner.WriteAsync(buffer, cancellationToken);
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    /// <summary>
    /// A server that answers at once with <paramref name="head" /> and sends
    /// <paramref name="rest" /> only after the request body has been written.
    /// </summary>
    private sealed class AnswersAfterTheBodyConnection(byte[] head, byte[] rest) : IConnection
    {
        private readonly MemoryStream written = new();

        private readonly TaskCompletionSource bodyWritten = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource restAwaited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly MemoryStream headToRead = new(head);

        private readonly MemoryStream restToRead = new(rest);

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        /// <summary>Completes once the whole head has been read and more is asked for.</summary>
        public Task RestAwaited => restAwaited.Task;

        public byte[] Written => written.ToArray();

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int read = headToRead.Read(buffer.Span);
            if (read > 0)
            {
                return read;
            }

            restAwaited.TrySetResult();
            await bodyWritten.Task.WaitAsync(cancellationToken);
            return restToRead.Read(buffer.Span);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            written.Write(buffer.Span);
            if (buffer.Span.IndexOf("x=1"u8) >= 0)
            {
                bodyWritten.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
