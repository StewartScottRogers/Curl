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
/// leaves it unsent. The clock is a fake one, advanced only for the 200.
/// </summary>
[TestClass]
public sealed class Http2ContinueWaitStatusTests
{
    private const string StopSending = "* HTTP error before end of send, stop sending";

    private const string AbortUpload = "* abort upload";

    private const int SettingsFrameLength = 9;

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
        ScriptedConnection connection = new(response, 65536);
        RecordingTransferEvents events = new();

        TransferResult result = await StartAsync(connection, events, new FakeTimeProvider(DateTimeOffset.UnixEpoch), followsRedirects);

        return (result, await BodyBytesSentAsync(connection.Written), events);
    }

    private static Task<TransferResult> StartAsync(
        IConnection connection,
        RecordingTransferEvents events,
        FakeTimeProvider time,
        bool followsRedirects = false)
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
                Headers = ["Expect: 100-continue"],
                Body = new BytesBody("x=1"u8.ToArray(), "application/x-www-form-urlencoded"),
            },
        };

        return new HttpProtocolHandler(QueueConnector.For(connection), new SilentAuthenticator())
            .ExecuteAsync(context).AsTask();
    }

    private static async Task<int> BodyBytesSentAsync(byte[] written)
    {
        const int PrefaceLength = 24;
        using MemoryStream frames = new(written, PrefaceLength, written.Length - PrefaceLength);
        int sent = 0;
        while (await Http2FrameCodec.ReadAsync(frames, 16384, CancellationToken.None) is { } frame)
        {
            sent += frame.Type == Http2FrameType.Data ? frame.Payload.Length : 0;
        }

        return sent;
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
