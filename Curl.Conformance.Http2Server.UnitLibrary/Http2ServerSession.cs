using System.Text;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http2Server;

/// <summary>
/// The server end of one h2c connection (ADR-0491): opened by the client's connection preface
/// (prior knowledge) or by an HTTP/1.1 request with <c>Upgrade: h2c</c>, answered with
/// <c>101 Switching Protocols</c> and then served as stream 1. Each stream's request, once its
/// END_STREAM arrives, is relayed to the backend as HTTP/1.1 and its reply sent back in HEADERS and
/// DATA frames within the client's flow-control windows. Never sends PUSH_PROMISE. Ends with
/// GOAWAY once the client sends GOAWAY or closes.
/// </summary>
/// <param name="stream">The server's half of the in-memory connection.</param>
/// <param name="backend">The HTTP/1.1 server each stream is relayed to.</param>
/// <param name="target">Where the client connected, passed on to <paramref name="backend"/>.</param>
/// <param name="forwardedProto">The <c>X-Forwarded-Proto</c> each relayed request carries: <c>http</c> for h2c, <c>https</c> behind the TLS front.</param>
internal sealed class Http2ServerSession(Stream stream, IConnector backend, ConnectTarget target, string forwardedProto = "http")
{
    private static readonly byte[] PrefaceHead = "PRI * HTTP/2.0\r\n\r\n"u8.ToArray();

    private static readonly byte[] SwitchingProtocols = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n"u8.ToArray();

    private readonly HpackDecoder decoder = new();

    private readonly HpackEncoder encoder = new();

    private readonly Http2Settings clientSettings = new();

    private readonly Dictionary<int, Http2ServerStream> streams = [];

    private readonly Queue<int> requestsReceived = new();

    private readonly MemoryStream headerBlock = new();

    private int connectionSendWindow = Http2Settings.DefaultInitialWindowSize;

    private int lastStreamId;

    private int headerBlockStreamId;

    private bool headerBlockEndsStream;

    /// <summary>
    /// Serves the connection until the client sends GOAWAY or closes, then sends GOAWAY and closes
    /// its end; closes at once when the opening is neither the preface nor an h2c upgrade.
    /// </summary>
    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        await using (stream.ConfigureAwait(false))
        {
            if (!await OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            while (await RespondToRequestsReceivedAsync(cancellationToken).ConfigureAwait(false)
                && await ReadAndHandleFrameAsync(cancellationToken).ConfigureAwait(false))
            {
            }

            await WriteAsync(Http2FrameFactory.CreateGoAway(lastStreamId, Http2ErrorCode.NoError, ReadOnlyMemory<byte>.Empty), cancellationToken).ConfigureAwait(false);
        }
    }

    // The preface's first line and the empty line after it read as one head, as an HTTP/1.1 request does.
    private async Task<bool> OpenAsync(CancellationToken cancellationToken)
    {
        byte[] head = await ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        if (head.AsSpan().SequenceEqual(PrefaceHead))
        {
            await stream.ReadExactlyAsync(new byte[6], cancellationToken).ConfigureAwait(false);
            await WriteServerSettingsAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (ToUpgradedRequest(Encoding.Latin1.GetString(head)) is not { } request)
        {
            return false;
        }

        await AcceptUpgradeAsync(request, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Turns an HTTP/1.1 request head into stream 1's HTTP/2 fields, or <see langword="null"/> when
    /// it is not a request or asks for no <c>h2c</c> upgrade.
    /// </summary>
    internal static IReadOnlyList<HeaderField>? ToUpgradedRequest(string head)
    {
        string[] lines = head.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] requestLine = lines.FirstOrDefault()?.Split(' ') ?? [];
        List<HeaderField> fields = [.. lines.Skip(1).Select(ToField)];
        return IsH2cUpgrade(requestLine, fields) ? ToStreamFields(requestLine, fields) : null;
    }

    private static bool IsH2cUpgrade(string[] requestLine, List<HeaderField> fields) =>
        requestLine.Length == 3 && fields.Contains(new("upgrade", "h2c"));

    private static List<HeaderField> ToStreamFields(string[] requestLine, List<HeaderField> fields) =>
        [new(":method", requestLine[0]), new(":scheme", "http"), new(":authority", fields.Find(IsHost).Value ?? string.Empty), new(":path", requestLine[1]), .. fields];

    private static bool IsHost(HeaderField field) => field.Name == "host";

    // 101, the server's SETTINGS, the client's preface; the upgrade request is then stream 1.
    private async Task AcceptUpgradeAsync(IReadOnlyList<HeaderField> request, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(SwitchingProtocols, cancellationToken).ConfigureAwait(false);
        await WriteServerSettingsAsync(cancellationToken).ConfigureAwait(false);
        await stream.ReadExactlyAsync(new byte[PrefaceHead.Length + 6], cancellationToken).ConfigureAwait(false);
        streams[1] = new Http2ServerStream(request);
        lastStreamId = 1;
        requestsReceived.Enqueue(1);
    }

    private static HeaderField ToField(string line)
    {
        int colon = Math.Max(line.IndexOf(':', StringComparison.Ordinal), 0);
        return new(line[..colon].Trim().ToLowerInvariant(), line[colon..].TrimStart(':').Trim());
    }

    // Bytes up to and including the first empty line; fewer when the client closes first.
    private async Task<byte[]> ReadHeadAsync(CancellationToken cancellationToken)
    {
        var head = new List<byte>();
        var next = new byte[1];
        while (!EndsWithEmptyLine(head) && await stream.ReadAsync(next, cancellationToken).ConfigureAwait(false) > 0)
        {
            head.Add(next[0]);
        }

        return [.. head];
    }

    private static bool EndsWithEmptyLine(List<byte> head) =>
        head.Count >= 4 && head[^4] == '\r' && head[^3] == '\n' && head[^2] == '\r' && head[^1] == '\n';

    private Task WriteServerSettingsAsync(CancellationToken cancellationToken) =>
        WriteAsync(Http2FrameFactory.CreateSettings([new(Http2SettingIdentifier.MaxConcurrentStreams, 100)]), cancellationToken);

    // False when the client went away part way through a reply.
    private async Task<bool> RespondToRequestsReceivedAsync(CancellationToken cancellationToken)
    {
        while (requestsReceived.TryDequeue(out int streamId))
        {
            if (!await RespondAsync(streamId, streams[streamId], cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> RespondAsync(int streamId, Http2ServerStream request, CancellationToken cancellationToken)
    {
        Http2ServerReply reply = await Http2ServerRequestRelay.RelayAsync(backend, target, request.Request, request.Body.ToArray(), forwardedProto, cancellationToken).ConfigureAwait(false);
        byte[] body = reply.Body;
        await WriteAsync(Http2FrameFactory.CreateHeaders(streamId, encoder.Encode(reply.Headers), isEndStream: body.Length == 0, isEndHeaders: true), cancellationToken).ConfigureAwait(false);
        int sent = 0;
        while (sent < body.Length)
        {
            int allowed = Math.Min(Math.Min(body.Length - sent, clientSettings.MaxFrameSize), Math.Min(connectionSendWindow, clientSettings.InitialWindowSize + request.SendWindowAdjustment));
            if (allowed <= 0)
            {
                if (!await ReadAndHandleFrameAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                continue;
            }

            await WriteAsync(Http2FrameFactory.CreateData(streamId, body.AsMemory(sent, allowed), isEndStream: sent + allowed == body.Length), cancellationToken).ConfigureAwait(false);
            connectionSendWindow -= allowed;
            request.SendWindowAdjustment -= allowed;
            sent += allowed;
        }

        return true;
    }

    // False when the client sent GOAWAY or closed.
    private async Task<bool> ReadAndHandleFrameAsync(CancellationToken cancellationToken)
    {
        Http2Frame? frame = await Http2FrameCodec.ReadAsync(stream, Http2FrameCodec.DefaultMaximumFrameSize, cancellationToken).ConfigureAwait(false);
        if (frame is null || frame.Type == Http2FrameType.GoAway)
        {
            return false;
        }

        await HandleFrameAsync(frame, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private Task HandleFrameAsync(Http2Frame frame, CancellationToken cancellationToken) => frame.Type switch
    {
        Http2FrameType.Settings => HandleSettingsAsync(frame, cancellationToken),
        Http2FrameType.Ping => HandlePingAsync(frame, cancellationToken),
        Http2FrameType.Data => HandleDataAsync(frame, cancellationToken),
        _ => HandleFrameWithoutReply(frame),
    };

    private Task HandlePingAsync(Http2Frame frame, CancellationToken cancellationToken) =>
        frame.HasFlag(Http2FrameFlags.Acknowledgement)
            ? Task.CompletedTask
            : WriteAsync(Http2FrameFactory.CreatePing(Http2FramePayloadParser.ParsePing(frame), isAcknowledgement: true), cancellationToken);

    // PRIORITY, RST_STREAM and anything unknown are ignored.
    private Task HandleFrameWithoutReply(Http2Frame frame)
    {
        switch (frame.Type)
        {
            case Http2FrameType.WindowUpdate:
                HandleWindowUpdate(frame);
                break;
            case Http2FrameType.Headers:
                headerBlockStreamId = frame.StreamId;
                headerBlockEndsStream = frame.HasFlag(Http2FrameFlags.EndStream);
                headerBlock.SetLength(0);
                ContinueHeaderBlock(frame, Http2FramePayloadParser.ParseHeaders(frame).Fragment);
                break;
            case Http2FrameType.Continuation:
                ContinueHeaderBlock(frame, frame.Payload);
                break;
        }

        return Task.CompletedTask;
    }

    private async Task HandleSettingsAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        if (frame.HasFlag(Http2FrameFlags.Acknowledgement))
        {
            return;
        }

        foreach (Http2Setting setting in Http2FramePayloadParser.ParseSettings(frame))
        {
            clientSettings.Apply(setting);
        }

        await WriteAsync(Http2FrameFactory.CreateSettingsAcknowledgement(), cancellationToken).ConfigureAwait(false);
    }

    private void HandleWindowUpdate(Http2Frame frame)
    {
        int increment = Http2FramePayloadParser.ParseWindowUpdate(frame);
        if (frame.StreamId == 0)
        {
            connectionSendWindow += increment;
        }
        else if (streams.TryGetValue(frame.StreamId, out Http2ServerStream? request))
        {
            request.SendWindowAdjustment += increment;
        }
    }

    // A second header block on a stream (trailers) is decoded and dropped; its END_STREAM still counts.
    private void ContinueHeaderBlock(Http2Frame frame, ReadOnlyMemory<byte> fragment)
    {
        headerBlock.Write(fragment.Span);
        if (!frame.HasFlag(Http2FrameFlags.EndHeaders))
        {
            return;
        }

        IReadOnlyList<HeaderField> fields = decoder.Decode(headerBlock.ToArray());
        if (streams.TryAdd(headerBlockStreamId, new Http2ServerStream(fields)))
        {
            lastStreamId = headerBlockStreamId;
        }

        EndStreamIf(headerBlockEndsStream, headerBlockStreamId);
    }

    // Every DATA byte is given back at once, on the connection and on the stream.
    private async Task HandleDataAsync(Http2Frame frame, CancellationToken cancellationToken)
    {
        streams[frame.StreamId].Body.Write(Http2FramePayloadParser.ParseData(frame).Span);
        if (frame.Payload.Length > 0)
        {
            await WriteAsync(Http2FrameFactory.CreateWindowUpdate(0, frame.Payload.Length), cancellationToken).ConfigureAwait(false);
            await WriteAsync(Http2FrameFactory.CreateWindowUpdate(frame.StreamId, frame.Payload.Length), cancellationToken).ConfigureAwait(false);
        }

        EndStreamIf(frame.HasFlag(Http2FrameFlags.EndStream), frame.StreamId);
    }

    private void EndStreamIf(bool isEndStream, int streamId)
    {
        if (isEndStream)
        {
            requestsReceived.Enqueue(streamId);
        }
    }

    private Task WriteAsync(Http2Frame frame, CancellationToken cancellationToken) =>
        Http2FrameCodec.WriteAsync(stream, frame, cancellationToken);
}
