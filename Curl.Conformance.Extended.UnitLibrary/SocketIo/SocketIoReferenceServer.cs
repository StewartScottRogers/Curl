using System.Text;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;

namespace Curl.Conformance.Extended.SocketIo;

/// <summary>
/// A reference Engine.IO v4 server on the WebSocket transport, written from the Engine.IO v4 protocol and RFC 6455, apart
/// from Curl's client. It reads one request; a WebSocket upgrade (one with a <c>Sec-WebSocket-Key</c>) is answered
/// <c>101</c> with the <c>Sec-WebSocket-Accept</c> that answers the key, and any other request <c>400</c>. After the
/// upgrade it plays <paramref name="script" /> (the Engine.IO and Socket.IO packets are the script's), then reads curl's
/// frames until curl closes the connection, answering curl's Close with a Close when the script sent none. It records
/// the request and every frame curl sent. The RFC 6455 framing is <see cref="SignalRReferenceFrame" />'s, the same
/// protocol-neutral codec the SignalR hub uses.
/// </summary>
/// <param name="script">The WebSocket script.</param>
public sealed class SocketIoReferenceServer(params IReadOnlyList<SocketIoStep> script) : IReferenceServer
{
    private const string BadRequest = "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";

    private readonly Lock gate = new();

    private readonly List<SignalRReferenceRequest> requests = [];

    private readonly List<SignalRReferenceFrame> frames = [];

    /// <summary>Gets the <c>Sec-WebSocket-Accept</c> value sent in place of the one that answers the key; <see langword="null" /> sends the right one.</summary>
    public string? AcceptOverride { get; init; }

    /// <summary>Gets every request received so far, in order.</summary>
    public IReadOnlyList<SignalRReferenceRequest> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    /// <summary>Gets every WebSocket frame curl sent so far, in order.</summary>
    public IReadOnlyList<SignalRReferenceFrame> ClientFrames
    {
        get
        {
            lock (gate)
            {
                return [.. frames];
            }
        }
    }

    /// <summary>Gets the payload of every text frame curl sent, as UTF-8, in order: curl's Engine.IO packets.</summary>
    public IReadOnlyList<string> ClientPackets =>
        [.. ClientFrames.Where(frame => frame.Opcode == SignalRReferenceFrame.Text).Select(frame => Encoding.UTF8.GetString(frame.Payload))];

    /// <inheritdoc />
    public async Task ServeAsync(ReferenceConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        SignalRReferenceReader reader = new(connection);
        if (await reader.ReadAsync<SignalRReferenceRequest>(SignalRReferenceRequest.TryRead, cancellationToken).ConfigureAwait(false) is not { } request)
        {
            return;
        }

        lock (gate)
        {
            requests.Add(request);
        }

        if (request.Header("Sec-WebSocket-Key") is not { } key)
        {
            await connection.WriteAsync(Encoding.ASCII.GetBytes(BadRequest), cancellationToken).ConfigureAwait(false);
            return;
        }

        string accept = AcceptOverride ?? SignalRReferenceHub.AcceptFor(key);
        await connection.WriteAsync(
            Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n"),
            cancellationToken).ConfigureAwait(false);
        await PlayAsync(connection, reader, cancellationToken).ConfigureAwait(false);
    }

    private async Task PlayAsync(ReferenceConnection connection, SignalRReferenceReader reader, CancellationToken cancellationToken)
    {
        bool closeSent = false;
        foreach (SocketIoStep step in script)
        {
            if (step.Frame is { } frame)
            {
                await connection.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                closeSent |= (frame[0] & 0x0F) == SignalRReferenceFrame.Close;
            }
            else if (await ReceiveAsync(reader, cancellationToken).ConfigureAwait(false) is null)
            {
                return;
            }
        }

        while (await ReceiveAsync(reader, cancellationToken).ConfigureAwait(false) is { } frame)
        {
            if (frame.Opcode == SignalRReferenceFrame.Close && !closeSent)
            {
                closeSent = true;
                await connection.WriteAsync(SignalRReferenceFrame.Write(0x80 | SignalRReferenceFrame.Close, frame.Payload), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<SignalRReferenceFrame?> ReceiveAsync(SignalRReferenceReader reader, CancellationToken cancellationToken)
    {
        SignalRReferenceFrame? frame = await reader.ReadAsync<SignalRReferenceFrame>(SignalRReferenceFrame.TryRead, cancellationToken).ConfigureAwait(false);
        if (frame is not null)
        {
            lock (gate)
            {
                frames.Add(frame);
            }
        }

        return frame;
    }
}
