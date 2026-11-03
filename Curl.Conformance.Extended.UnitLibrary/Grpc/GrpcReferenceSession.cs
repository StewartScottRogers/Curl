using Curl.Conformance.Extended.Harness;
using Curl.Http2;

namespace Curl.Conformance.Extended.Grpc;

/// <summary>
/// One connection of <see cref="GrpcReferenceServer" />: the HPACK encoder and decoder that span its streams, its calls by
/// stream, and the header block a HEADERS frame started until its END_HEADERS.
/// </summary>
internal sealed class GrpcReferenceSession(ReferenceConnection connection, IReadOnlyList<IGrpcReferenceService> services, Action<GrpcReferenceCall> record)
{
    private const int UnimplementedStatus = 12;

    private readonly HpackEncoder encoder = new();

    private readonly HpackDecoder decoder = new();

    private readonly Dictionary<int, GrpcReferenceCall> calls = [];

    private readonly List<byte> headerBlock = [];

    private readonly List<GrpcReferencePing> pings = [];

    private bool headerBlockEndsStream;

    /// <summary>Serves the connection until the client closes it.</summary>
    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        if (!await GrpcReferenceFrameReader.ReadPrefaceAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await WriteAsync(Http2FrameFactory.CreateSettings([]), cancellationToken).ConfigureAwait(false);
        while (await GrpcReferenceFrameReader.ReadAsync(connection, cancellationToken).ConfigureAwait(false) is { } frame)
        {
            await ReceiveAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task ReceiveAsync(Http2Frame frame, CancellationToken cancellationToken) => frame.HasFlag(Http2FrameFlags.Acknowledgement) && frame.StreamId == 0
        ? ReceiveAcknowledgementAsync(frame)
        : ReceiveUnacknowledgedAsync(frame, cancellationToken);

    // A PING acknowledgement is offered to every PING the server sent, which records it when it echoes that PING's payload.
    private Task ReceiveAcknowledgementAsync(Http2Frame frame)
    {
        if (frame.Type == Http2FrameType.Ping)
        {
            ulong echoed = Http2FramePayloadParser.ParsePing(frame);
            pings.ForEach(ping => ping.RecordEcho(echoed));
        }

        return Task.CompletedTask;
    }

    // A SETTINGS or PING acknowledgement (flag 0x1 on stream 0) needs no answer; the same bit on a stream frame is END_STREAM.
    private Task ReceiveUnacknowledgedAsync(Http2Frame frame, CancellationToken cancellationToken) => frame.Type switch
    {
        Http2FrameType.Settings => WriteAsync(Http2FrameFactory.CreateSettingsAcknowledgement(), cancellationToken),
        Http2FrameType.Ping => WriteAsync(Http2FrameFactory.CreatePing(Http2FramePayloadParser.ParsePing(frame), isAcknowledgement: true), cancellationToken),
        Http2FrameType.Headers => ReceiveHeaderBlockAsync(frame, Http2FramePayloadParser.ParseHeaders(frame).Fragment, frame.HasFlag(Http2FrameFlags.EndStream), cancellationToken),
        Http2FrameType.Continuation => ReceiveHeaderBlockAsync(frame, frame.Payload, headerBlockEndsStream, cancellationToken),
        Http2FrameType.Data => ReceiveDataAsync(frame, cancellationToken),
        _ => Task.CompletedTask,
    };

    private async Task ReceiveHeaderBlockAsync(Http2Frame frame, ReadOnlyMemory<byte> fragment, bool endsStream, CancellationToken cancellationToken)
    {
        headerBlock.AddRange(fragment.Span);
        headerBlockEndsStream = endsStream;
        if (!frame.HasFlag(Http2FrameFlags.EndHeaders))
        {
            return;
        }

        GrpcReferenceCall call = new(frame.StreamId, decoder.Decode([.. headerBlock]));
        headerBlock.Clear();
        calls[call.StreamId] = call;
        record(call);
        await ReceiveRequestPartAsync(call, endsStream, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReceiveDataAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        if (calls.TryGetValue(frame.StreamId, out GrpcReferenceCall? call))
        {
            call.AddData(Http2FramePayloadParser.ParseData(frame));
            await ReceiveRequestPartAsync(call, frame.HasFlag(Http2FrameFlags.EndStream), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceiveRequestPartAsync(GrpcReferenceCall call, bool endsStream, CancellationToken cancellationToken)
    {
        if (endsStream)
        {
            call.EndRequest();
        }

        if (!call.IsAnswered && AnswerOf(call) is { } reply)
        {
            call.IsAnswered = true;
            await SendReplyAsync(call.StreamId, reply, cancellationToken).ConfigureAwait(false);
        }
    }

    private GrpcReferenceReply? AnswerOf(GrpcReferenceCall call)
    {
        foreach (IGrpcReferenceService service in services)
        {
            if (service.Answer(call) is { } reply)
            {
                return reply;
            }
        }

        return call.IsRequestEnded ? GrpcReferenceReply.TrailersOnly(UnimplementedStatus, $"No service serves {call.Path}") : null;
    }

    private async Task SendReplyAsync(int streamId, GrpcReferenceReply reply, CancellationToken cancellationToken)
    {
        if (reply.PingBeforeReply is { } ping)
        {
            pings.Add(ping);
            await WriteAsync(Http2FrameFactory.CreatePing(ping.Payload, isAcknowledgement: false), cancellationToken).ConfigureAwait(false);
        }

        await (reply.ResetCode is { } errorCode
            ? WriteAsync(Http2FrameFactory.CreateRstStream(streamId, errorCode), cancellationToken)
            : reply.GoAwayLastStreamId is { } lastStreamId
                ? WriteAsync(Http2FrameFactory.CreateGoAway(lastStreamId, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty), cancellationToken)
                : SendResponseAsync(streamId, reply, cancellationToken)).ConfigureAwait(false);
    }

    private async Task SendResponseAsync(int streamId, GrpcReferenceReply reply, CancellationToken cancellationToken)
    {
        if (reply.IsTrailersOnly)
        {
            await WriteHeadersAsync(streamId, [.. reply.Headers, .. reply.Trailers], isEndStream: true, cancellationToken).ConfigureAwait(false);
            return;
        }

        await WriteHeadersAsync(streamId, reply.Headers, isEndStream: false, cancellationToken).ConfigureAwait(false);
        foreach (byte[] payload in reply.DataFrames)
        {
            await WriteAsync(Http2FrameFactory.CreateData(streamId, payload, isEndStream: false), cancellationToken).ConfigureAwait(false);
        }

        await (reply.Trailers.Count > 0
            ? WriteHeadersAsync(streamId, reply.Trailers, isEndStream: true, cancellationToken)
            : WriteAsync(Http2FrameFactory.CreateData(streamId, ReadOnlyMemory<byte>.Empty, isEndStream: true), cancellationToken)).ConfigureAwait(false);
    }

    private Task WriteHeadersAsync(int streamId, IReadOnlyList<HeaderField> headers, bool isEndStream, CancellationToken cancellationToken) =>
        WriteAsync(Http2FrameFactory.CreateHeaders(streamId, encoder.Encode(headers), isEndStream, isEndHeaders: true), cancellationToken);

    private Task WriteAsync(Http2Frame frame, CancellationToken cancellationToken) =>
        connection.WriteAsync(Http2FrameCodec.Serialize(frame), cancellationToken).AsTask();
}
