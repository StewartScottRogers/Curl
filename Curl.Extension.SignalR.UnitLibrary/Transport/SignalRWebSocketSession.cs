using Curl.Extension.SignalR.HubProtocol;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.SignalR.Transport;

/// <summary>
/// A SignalR connection over an upgraded WebSocket (TransportProtocols.md, "WebSockets (Full Duplex)"): sends hub
/// protocol records as masked text frames and reads them back from the server's text frames, answering a Ping with a
/// Pong carrying its payload (RFC 6455 section 5.5.2) and a Close with a Close carrying its status code (section 5.5.1).
/// </summary>
/// <param name="connection">The upgraded connection.</param>
/// <param name="reader">The reader that read the upgrade response, holding any frame bytes that came with it.</param>
/// <param name="randomSource">The source of each frame's mask.</param>
internal sealed class SignalRWebSocketSession(IConnection connection, BufferedConnectionReader reader, IWebSocketRandomSource randomSource)
{
    private const int StatusCodeLength = 2;

    private readonly WsFrameDecoder decoder = new(recordsRuns: true);

    private readonly HubFrameSplitter splitter = new();

    private readonly List<byte> closePayload = [];

    /// <summary>Whether the server sent a Close frame, which this session has answered.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Sends <paramref name="message"/>, one or more hub protocol records, as one masked text frame.</summary>
    public Task SendTextAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken) =>
        SendFrameAsync(WsOpcode.Text, message.Span.ToArray(), cancellationToken);

    /// <summary>
    /// Reads the next hub protocol record, without its 0x1E, from the server's text frames; <see langword="null"/>
    /// once the server has closed the WebSocket and every record before its Close has been read.
    /// </summary>
    /// <exception cref="SignalRTransportException">A frame breaks RFC 6455, or the connection ended without a Close.</exception>
    public async Task<byte[]?> ReceiveRecordAsync(CancellationToken cancellationToken)
    {
        byte[] record;
        while (!splitter.TryTakeRecord(out record))
        {
            if (IsClosed)
            {
                return null;
            }

            await ReceiveFramesAsync(cancellationToken);
        }

        return record;
    }

    private async Task ReceiveFramesAsync(CancellationToken cancellationToken)
    {
        byte[] received = await reader.ReadAvailableAsync(cancellationToken);
        if (received.Length == 0)
        {
            throw new SignalRTransportException("The server closed the connection without a WebSocket Close frame.");
        }

        WsDecodedBytes decoded = decoder.Decode(received);
        if (decoded.ViolationMessage is not null)
        {
            throw new SignalRTransportException(decoded.ViolationMessage);
        }

        if (decoded.LastPing is not null)
        {
            await SendFrameAsync(WsOpcode.Pong, decoded.LastPing, cancellationToken);
        }

        foreach (WsFrameRun run in decoder.TakeRuns())
        {
            await AcceptRunAsync(run, cancellationToken);
        }
    }

    private async Task AcceptRunAsync(WsFrameRun run, CancellationToken cancellationToken)
    {
        switch (run.Opcode)
        {
            case WsOpcode.Text or WsOpcode.Continuation:
                splitter.Append(run.Payload.Span);
                break;
            case WsOpcode.Close:
                closePayload.AddRange(run.Payload.Span);
                if (run.BytesLeft == 0)
                {
                    await SendFrameAsync(WsOpcode.Close, [.. closePayload.Take(StatusCodeLength)], cancellationToken);
                    IsClosed = true;
                }

                break;
        }
    }

    private async Task SendFrameAsync(WsOpcode opcode, byte[] payload, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(WsFrameEncoder.Encode(opcode, payload, randomSource), cancellationToken);
        await connection.FlushAsync(cancellationToken);
    }
}
