using Curl.Extended.Protocol.Grpc.Wire;
using Curl.Http2;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Grpc.Client;

/// <summary>
/// Makes one gRPC call on its own stream of an HTTP/2 connection (PROTOCOL-HTTP2.md "Requests" and "Responses"): sends the
/// Request-Headers and one Length-Prefixed-Message ending the stream, and reads the Response-Headers, every
/// Length-Prefixed-Message and the Trailers, or a Trailers-Only response.
/// </summary>
internal static class GrpcHttp2Call
{
    /// <summary>Makes the call.</summary>
    /// <param name="session">The HTTP/2 connection the call opens its stream on; the caller owns it.</param>
    /// <param name="headers">The Request-Headers, names lower case, pseudo-headers first.</param>
    /// <param name="message">The request message's binary form.</param>
    /// <param name="onMessage">Takes each response message as soon as it is decoded, before the next frame is read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The response's status and head report; the messages went to <paramref name="onMessage" />.</returns>
    /// <exception cref="GrpcWireException">The response breaks PROTOCOL-HTTP2.md.</exception>
    /// <exception cref="GrpcClientException">The server closed the connection before the call ended (exit 16).</exception>
    public static async Task<GrpcCallResult> RunAsync(GrpcHttp2Session session, IReadOnlyList<KeyValuePair<string, string>> headers, byte[] message, Func<GrpcMessage, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        int streamId = await session.OpenStreamAsync(headers, cancellationToken).ConfigureAwait(false);
        Http2StreamFrame? early = await SendMessageAsync(session.Connection, streamId, LengthPrefixedMessage.Frame(message, isCompressed: false), cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync(session, early, onMessage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends the framed message as the window allows, reading the peer's frames for WINDOW_UPDATE while it waits.
    /// </summary>
    /// <returns>A response frame that came before the message was all sent, which ends the sending; otherwise <see langword="null" />.</returns>
    private static async Task<Http2StreamFrame?> SendMessageAsync(Http2Connection connection, int streamId, ReadOnlyMemory<byte> framed, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte> unsent = framed;
        while (true)
        {
            unsent = unsent[await connection.WriteDataAsync(streamId, unsent, isEndStream: true, cancellationToken).ConfigureAwait(false)..];
            if (unsent.IsEmpty)
            {
                return null;
            }

            Http2StreamFrame? frame = await connection.ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            if (frame is not null || connection.IsClosedByPeer)
            {
                return frame;
            }
        }
    }

    private static async Task<GrpcCallResult> ReadResponseAsync(GrpcHttp2Session session, Http2StreamFrame? early, Func<GrpcMessage, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        Http2Connection connection = session.Connection;
        var response = new ResponseReader(session.Decoder);
        Http2StreamFrame? frame = early ?? await connection.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false);
        int delivered = 0;
        while (frame is not null)
        {
            GrpcStatus? status = response.Read(frame);
            for (; delivered < response.Messages.Count; delivered++)
            {
                await onMessage(response.Messages[delivered]).ConfigureAwait(false);
            }

            if (status is { } ended)
            {
                return new GrpcCallResult(ended, response.Report);
            }

            frame = await connection.ReadStreamFrameAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new GrpcClientException(Curl.Protocol.Abstractions.CurlExitCode.Http2, "The server closed the HTTP/2 connection before the gRPC call ended");
    }

    /// <summary>Reads one response's stream frames in order: the Response-Headers, the messages and the Trailers.</summary>
    /// <param name="decoder">The connection's HPACK decoder.</param>
    internal sealed class ResponseReader(HpackDecoder decoder)
    {
        private readonly LengthPrefixedMessageReader reader = new();
        private bool isHeadRead;

        /// <summary>Gets the messages read so far.</summary>
        public List<GrpcMessage> Messages { get; } = [];

        /// <summary>Gets what <c>-w</c> reports about the response head, or an empty report before it is read.</summary>
        public TransferReport Report { get; private set; } = new();

        /// <summary>Reads one frame; gives the call's status when the frame ended it, otherwise <see langword="null" />.</summary>
        public GrpcStatus? Read(Http2StreamFrame frame)
        {
            GrpcStatus? status = frame.Type == Http2FrameType.Headers ? ReadHeaders(frame) : ReadMessages(frame);
            if (status is null && frame.IsEndStream)
            {
                throw new GrpcWireException("A gRPC response ended without trailers.");
            }

            return status;
        }

        private GrpcStatus? ReadHeaders(Http2StreamFrame frame)
        {
            IReadOnlyList<KeyValuePair<string, string>> fields = [.. decoder.Decode(frame.Content.Span).Select(static field => new KeyValuePair<string, string>(field.Name, field.Value))];
            if (isHeadRead)
            {
                reader.Complete();
                return GrpcResponseHeaders.ReadTrailers(fields);
            }

            isHeadRead = true;
            GrpcResponseHead head = GrpcResponseHeaders.ReadHeaders(fields);
            Report = ReportOf(head.HttpStatus, fields);
            return head.FinalStatus;
        }

        // Sized as curl counts an HTTP/2 head: the status line "HTTP/2 200 ", each field as "name: value", then an empty line, all CRLF-ended (ADR-1260).
        private static TransferReport ReportOf(int httpStatus, IReadOnlyList<KeyValuePair<string, string>> fields)
        {
            long size = $"HTTP/2 {httpStatus} \r\n".Length + 2;
            foreach (KeyValuePair<string, string> field in fields.Where(static field => !field.Key.StartsWith(':')))
            {
                size += field.Key.Length + field.Value.Length + 4;
            }

            string? contentType = fields.FirstOrDefault(static field => field.Key == "content-type").Value;
            return new TransferReport { ResponseCode = httpStatus, HttpVersion = new Version(2, 0), ContentType = contentType, HeaderSize = size };
        }

        private GrpcStatus? ReadMessages(Http2StreamFrame frame)
        {
            reader.Append(frame.Content.Span);
            while (reader.TryRead(out GrpcMessage? message))
            {
                Messages.Add(message);
            }

            return null;
        }
    }
}
