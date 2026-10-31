using System.Threading.Channels;
using Curl.Extension.WebTransport.Wire;
using Curl.Http2;
using Curl.Http3;
using Curl.HttpDatagram;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.WebTransport.Session;

/// <summary>
/// One WebTransport session over an HTTP/3 connection (draft-ietf-webtrans-http3-16, ADR-1089): waits
/// for the server's SETTINGS (section 3.1), opens the session with an extended CONNECT (section 3.2),
/// sends the data on a stream or in a datagram (sections 4.2, 4.3 and 4.5), writes the server's answer
/// to the output, and closes the session with <c>WT_CLOSE_SESSION</c> (section 6).
/// </summary>
/// <param name="connection">The HTTP/3 connection, past its QUIC handshake.</param>
/// <param name="context">The transfer, whose output gets the answer.</param>
internal sealed class WebTransportSession(IMultiplexedConnection connection, ITransferContext context)
{
    private const int BufferLength = 16 * 1024;

    private readonly Channel<IMultiplexedStream> peerUnidirectionalStreams = Channel.CreateUnbounded<IMultiplexedStream>();

    private readonly TaskCompletionSource<Http3SettingsFrame> peerSettings = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Runs the session.</summary>
    /// <param name="request">What the transfer sends and how.</param>
    /// <returns>How many bytes were written to the output.</returns>
    public async ValueTask<long> RunAsync(WebTransportRequest request)
    {
        CancellationToken cancellationToken = context.CancellationToken;
        await using Http3ClientSession http3 = new(connection, WebTransportWire.ClientSettings, OnControlFrame, peer => TakePeerStream(peer, request.Mode));
        _ = http3.PeerStreamsReading.ContinueWith(_ => peerSettings.TrySetException(new WebTransportException(CurlExitCode.RecvError, "The HTTP/3 connection failed before the server's SETTINGS")), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        Http3ClientRequestStream connect = await http3.OpenRequestStreamAsync(null, cancellationToken).ConfigureAwait(false);
        if (!WebTransportWire.AllowsSessions(await peerSettings.Task.WaitAsync(cancellationToken).ConfigureAwait(false)))
        {
            throw new WebTransportException(CurlExitCode.WeirdServerReply, "The server does not offer WebTransport over HTTP/3");
        }

        await connect.SendHeadersAsync(ConnectFields(request), endStream: false, cancellationToken).ConfigureAwait(false);
        await ReadSessionResponseAsync(connect, cancellationToken).ConfigureAwait(false);
        long written = await ExchangeAsync(request, connect.StreamId, cancellationToken).ConfigureAwait(false);
        await connect.SendDataAsync(new WebTransportCloseSession(0, string.Empty).ToCapsuleBytes(), endStream: true, cancellationToken).ConfigureAwait(false);
        return written;
    }

    /// <summary>Gets the extended CONNECT's field section (section 3.2, RFC 9220 section 3).</summary>
    /// <param name="request">The request.</param>
    /// <returns>The fields.</returns>
    internal static IReadOnlyList<HeaderField> ConnectFields(WebTransportRequest request) =>
    [
        new(":method", "CONNECT"),
        new(":protocol", WebTransportWire.ProtocolToken),
        new(":scheme", "https"),
        new(":authority", request.Authority),
        new(":path", request.Path),
    ];

    private void OnControlFrame(Http3Frame frame)
    {
        if (frame is Http3SettingsFrame settings)
        {
            peerSettings.TrySetResult(settings);
        }
    }

    /// <summary>Tells whether a stream the server opened is a WebTransport unidirectional stream a <paramref name="mode" /> transfer reads (section 4.2).</summary>
    /// <param name="peer">The server's stream.</param>
    /// <param name="mode">How the transfer carries its data.</param>
    /// <returns><see langword="true" /> for a unidirectional stream of type <c>0x54</c> in a unidirectional transfer.</returns>
    internal static bool IsSessionUnidirectionalStream(Http3UnknownPeerStream peer, WebTransportMode mode) =>
        mode == WebTransportMode.Unidirectional && !peer.IsBidirectional && peer.Type == WebTransportWire.UnidirectionalStreamType;

    /// <summary>Tells whether a <c>:status</c> accepts the session: any 2xx (section 3.3).</summary>
    /// <param name="status">The response's <c>:status</c>, or <see langword="null" /> when it has none.</param>
    /// <returns><see langword="true" /> for a three-character status starting with 2.</returns>
    internal static bool IsSuccessStatus(string? status) => status is ['2', _, _];

    private bool TakePeerStream(Http3UnknownPeerStream peer, WebTransportMode mode)
    {
        if (!IsSessionUnidirectionalStream(peer, mode))
        {
            return false;
        }

        _ = peerUnidirectionalStreams.Writer.TryWrite(peer.Stream);
        return true;
    }

    private static async ValueTask ReadSessionResponseAsync(Http3ClientRequestStream connect, CancellationToken cancellationToken)
    {
        Http3HeadersFrame headers = await ReadResponseHeadersAsync(connect, cancellationToken).ConfigureAwait(false);
        string? status = connect.DecodeFieldSection(headers).FirstOrDefault(field => field.Name == ":status").Value;
        if (!IsSuccessStatus(status))
        {
            throw new WebTransportException(CurlExitCode.HttpReturnedError, $"The server refused the WebTransport session: HTTP status {status}");
        }
    }

    private static async ValueTask<Http3HeadersFrame> ReadResponseHeadersAsync(Http3ClientRequestStream connect, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferLength];
        while (true)
        {
            Http3FrameOrData read = await connect.ReadFrameOrDataAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read.IsEndOfStream)
            {
                throw new WebTransportException(CurlExitCode.RecvError, "The server ended the session stream before answering the CONNECT");
            }

            if (read.Frame is Http3HeadersFrame headers)
            {
                return headers;
            }
        }
    }

    private ValueTask<long> ExchangeAsync(WebTransportRequest request, long sessionId, CancellationToken cancellationToken) => request.Mode switch
    {
        WebTransportMode.Unidirectional => ExchangeOnUnidirectionalStreamsAsync(request.Data, sessionId, cancellationToken),
        WebTransportMode.Datagram => ExchangeDatagramsAsync(request.Data, sessionId, cancellationToken),
        _ => ExchangeOnBidirectionalStreamAsync(request.Data, sessionId, cancellationToken),
    };

    private async ValueTask<long> ExchangeOnBidirectionalStreamAsync(ReadOnlyMemory<byte> data, long sessionId, CancellationToken cancellationToken)
    {
        await using IMultiplexedStream stream = await connection.OpenBidirectionalStreamAsync(cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(WebTransportWire.StreamHeaderAndData(WebTransportWire.BidirectionalSignal, sessionId, data.Span), endStream: true, cancellationToken).ConfigureAwait(false);
        return await CopyToOutputAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<long> ExchangeOnUnidirectionalStreamsAsync(ReadOnlyMemory<byte> data, long sessionId, CancellationToken cancellationToken)
    {
        await using (IMultiplexedStream stream = await connection.OpenUnidirectionalStreamAsync(cancellationToken).ConfigureAwait(false))
        {
            await stream.WriteAsync(WebTransportWire.StreamHeaderAndData(WebTransportWire.UnidirectionalStreamType, sessionId, data.Span), endStream: true, cancellationToken).ConfigureAwait(false);
        }

        while (true)
        {
            IMultiplexedStream peer = await peerUnidirectionalStreams.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (await ReadVariableLengthIntegerAsync(peer, cancellationToken).ConfigureAwait(false) == sessionId)
            {
                return await CopyToOutputAsync(peer, cancellationToken).ConfigureAwait(false);
            }

            peer.Abort(WebTransportWire.BufferedStreamRejected);
        }
    }

    private async ValueTask<long> ExchangeDatagramsAsync(ReadOnlyMemory<byte> data, long sessionId, CancellationToken cancellationToken)
    {
        if (connection is not IDatagramCarryingConnection { MaxDatagramPayloadLength: > 0 } datagrams)
        {
            throw new WebTransportException(CurlExitCode.WeirdServerReply, "The server does not accept QUIC datagrams");
        }

        byte[] datagram = Http3Datagram.Encode((ulong)sessionId, data.Span);
        if (datagram.Length > datagrams.MaxDatagramPayloadLength)
        {
            throw new WebTransportException(CurlExitCode.SendError, $"The data does not fit in one datagram: at most {datagrams.MaxDatagramPayloadLength} bytes, quarter stream ID included");
        }

        await datagrams.SendDatagramAsync(datagram, cancellationToken).ConfigureAwait(false);
        while (true)
        {
            ReadOnlyMemory<byte> payload = Http3Datagram.Decode(await datagrams.ReceiveDatagramAsync(cancellationToken).ConfigureAwait(false), out ulong streamId);
            if (streamId == (ulong)sessionId)
            {
                await context.Output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                return payload.Length;
            }
        }
    }

    private async ValueTask<long> CopyToOutputAsync(IMultiplexedStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[BufferLength];
        long written = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await context.Output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            written += read;
        }

        return written;
    }

    private static async ValueTask<long> ReadVariableLengthIntegerAsync(IMultiplexedStream stream, CancellationToken cancellationToken)
    {
        byte[] first = new byte[1];
        await ReadExactlyAsync(stream, first, cancellationToken).ConfigureAwait(false);
        byte[] encoding = new byte[1 << (first[0] >> 6)];
        encoding[0] = first[0];
        await ReadExactlyAsync(stream, encoding.AsMemory(1), cancellationToken).ConfigureAwait(false);
        HttpDatagramVariableLengthInteger.TryRead(encoding, out ulong value, out _);
        return (long)value;
    }

    private static async ValueTask ReadExactlyAsync(IMultiplexedStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        for (int filled = 0; filled < buffer.Length;)
        {
            int read = await stream.ReadAsync(buffer[filled..], cancellationToken).ConfigureAwait(false);
            filled += read > 0 ? read : throw new WebTransportException(CurlExitCode.RecvError, "A server stream ended inside its WebTransport header");
        }
    }
}
