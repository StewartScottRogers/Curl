using Curl.Conformance.Http2Server;
using Curl.Http2;
using Curl.Http3;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// HTTP/3 (RFC 9114) on one <see cref="Http3ServerConnection" />, as nghttpx-h3 serves it in front
/// of <c>sws</c>: once the handshake completes it opens its control stream with an empty SETTINGS
/// frame (so QPACK's dynamic table stays at capacity 0 both ways, RFC 9204 section 3.2.3), and
/// each request stream, once its FIN has arrived, is decoded (HEADERS, then any DATA) and relayed
/// to the backend by <see cref="Http3ServerRequestRelay" />; the reply goes back as one HEADERS
/// frame, one DATA frame when it has a body, and the FIN. The client's unidirectional streams
/// (control and QPACK) are read and ignored. A request stream that does not decode closes the
/// connection with <c>H3_MESSAGE_ERROR</c>.
/// </summary>
/// <param name="connection">The QUIC connection, which the session owns and disposes.</param>
/// <param name="backend">The HTTP/1.1 server each request is relayed to, the case's sws emulation.</param>
/// <param name="target">Where the backend connections go.</param>
internal sealed class Http3ServerSession(Http3ServerConnection connection, IConnector backend, ConnectTarget target) : IDisposable
{
    // H3_MESSAGE_ERROR (RFC 9114 section 8.1): a malformed request.
    internal const ulong MessageError = 0x010e;

    private const long MaximumFramePayloadLength = 1L << 30;

    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly List<Http3ServerStream> requests = [];

    private readonly Dictionary<ulong, List<byte>> requestBytes = [];

    private readonly QpackDecoder decoder = new(0, 0);

    private readonly QpackEncoder encoder = new(0, 0);

    private bool controlStreamOpened;

    /// <summary>Gets the QUIC connection.</summary>
    internal Http3ServerConnection Connection => connection;

    /// <summary>
    /// Takes one datagram from the client, answers every request it completes, and returns the
    /// datagrams to send. Calls are served one at a time.
    /// </summary>
    internal async Task<IReadOnlyList<byte[]>> ReceiveAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<byte[]> toSend = [.. connection.Receive(datagram)];
            await ServeAsync(toSend, cancellationToken).ConfigureAwait(false);
            return toSend;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        connection.Dispose();
        gate.Dispose();
    }

    private async Task ServeAsync(List<byte[]> toSend, CancellationToken cancellationToken)
    {
        if (!connection.IsHandshakeComplete || connection.IsClosed)
        {
            return;
        }

        OpenControlStream();
        AcceptRequests();
        await AnswerCompleteRequestsAsync(toSend, cancellationToken).ConfigureAwait(false);
        toSend.AddRange(connection.IsClosed ? [] : connection.TakeDatagramsToSend());
    }

    // Answers each request stream whose FIN has arrived, in the order the client opened them,
    // until one closes the connection.
    private async Task AnswerCompleteRequestsAsync(List<byte[]> toSend, CancellationToken cancellationToken)
    {
        foreach (Http3ServerStream stream in requests.ToList())
        {
            requestBytes[stream.Id].AddRange(stream.Read());
            if (stream.IsReadComplete && !connection.IsClosed)
            {
                requests.Remove(stream);
                await AnswerAsync(stream.Id, toSend, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void OpenControlStream()
    {
        if (!controlStreamOpened)
        {
            controlStreamOpened = true;
            connection.Write(connection.OpenUnidirectionalStream(), [(byte)Http3UnidirectionalStreamType.Control, .. new Http3SettingsFrame([]).ToBytes()], endStream: false);
        }
    }

    private void AcceptRequests()
    {
        while (connection.AcceptStream() is { } stream)
        {
            if (!stream.IsUnidirectional)
            {
                requests.Add(stream);
                requestBytes[stream.Id] = [];
            }
        }
    }

    private async Task AnswerAsync(ulong streamId, List<byte[]> toSend, CancellationToken cancellationToken)
    {
        byte[] bytes = [.. requestBytes[streamId]];
        requestBytes.Remove(streamId);
        if (await ReadRequestAsync(streamId, bytes, cancellationToken).ConfigureAwait(false) is not { } request)
        {
            toSend.AddRange(connection.Close(MessageError));
            return;
        }

        Http2ServerReply reply = await Http3ServerRequestRelay.RelayAsync(backend, target, request.Fields, request.Body, cancellationToken).ConfigureAwait(false);
        connection.Write(streamId, new Http3HeadersFrame(encoder.EncodeFieldSection((long)streamId, reply.Headers)).ToBytes(), endStream: false);
        connection.Write(streamId, reply.Body.Length > 0 ? new Http3DataFrame(reply.Body).ToBytes() : [], endStream: true);
    }

    // The request's fields from its HEADERS frame and its body from the DATA frames after it, or
    // null when the stream breaks HTTP/3 or QPACK or carries no HEADERS frame.
    private async Task<(IReadOnlyList<HeaderField> Fields, byte[] Body)?> ReadRequestAsync(ulong streamId, byte[] bytes, CancellationToken cancellationToken)
    {
        var reader = new Http3FrameReader(new MemoryStream(bytes), MaximumFramePayloadLength);
        IReadOnlyList<HeaderField>? fields = null;
        var body = new MemoryStream();
        try
        {
            while (await reader.ReadFrameAsync(cancellationToken).ConfigureAwait(false) is { } frame)
            {
                fields ??= frame is Http3HeadersFrame headers ? Decode(streamId, headers) : null;
                body.Write(frame is Http3DataFrame data ? data.Payload.Span : []);
            }
        }
        catch (Exception error) when (error is Http3Exception or QpackException)
        {
            return null;
        }

        return fields is null ? null : (fields, body.ToArray());
    }

    // With no dynamic table a field section never blocks, so a section that fails to decode throws.
    private IReadOnlyList<HeaderField> Decode(ulong streamId, Http3HeadersFrame headers)
    {
        decoder.TryDecodeFieldSection((long)streamId, headers.EncodedFieldSection.Span, out IReadOnlyList<HeaderField>? fields);
        return fields!;
    }
}
