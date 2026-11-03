using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.SignalR;

/// <summary>
/// A reference SignalR endpoint written from ASP.NET Core's TransportProtocols.md and RFC 6455, apart from Curl's
/// client. Each connection carries one request: a <c>POST .../negotiate</c> is answered <c>200</c> with the next of
/// <paramref name="negotiateBodies" /> (the last one again once they run out), and a WebSocket upgrade with
/// <c>101</c> and the <c>Sec-WebSocket-Accept</c> that answers its key, after which the hub plays
/// <paramref name="script" /> and then reads curl's frames until curl closes the connection, answering curl's Close
/// with a Close when the script sent none. It records every request and every frame curl sent, across connections.
/// The hub protocol is the script's: a case writes the records (HubProtocol.md) it sends.
/// </summary>
/// <param name="negotiateBodies">The negotiate response bodies, in order; at least one.</param>
/// <param name="script">The WebSocket script.</param>
public sealed class SignalRReferenceHub(IReadOnlyList<string> negotiateBodies, params IReadOnlyList<SignalRHubStep> script) : IReferenceServer
{
    private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly Lock gate = new();

    private readonly List<SignalRReferenceRequest> requests = [];

    private readonly List<SignalRReferenceFrame> frames = [];

    private int negotiations;

    /// <summary>Gets the <c>Sec-WebSocket-Accept</c> value the hub sends in place of the one that answers the key; <see langword="null" /> sends the right one.</summary>
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

    /// <summary>Gets the payload of every text frame curl sent, as UTF-8, in order.</summary>
    public IReadOnlyList<string> ClientTexts =>
        [.. ClientFrames.Where(frame => frame.Opcode == SignalRReferenceFrame.Text).Select(frame => Encoding.UTF8.GetString(frame.Payload))];

    /// <summary>The <c>Sec-WebSocket-Accept</c> value that answers <paramref name="key" /> (RFC 6455 section 4.2.2).</summary>
    /// <param name="key">The client's <c>Sec-WebSocket-Key</c>.</param>
    /// <returns>The accept value.</returns>
    public static string AcceptFor(string key) =>
        Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + AcceptGuid)));

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

        if (request.Header("Sec-WebSocket-Key") is { } key)
        {
            await connection.WriteAsync(Encoding.ASCII.GetBytes(SwitchingProtocols(key)), cancellationToken).ConfigureAwait(false);
            await PlayAsync(connection, reader, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await connection.WriteAsync(Encoding.UTF8.GetBytes(Negotiated(NextNegotiateBody())), cancellationToken).ConfigureAwait(false);
        }
    }

    private string SwitchingProtocols(string key) =>
        $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {AcceptOverride ?? AcceptFor(key)}\r\n\r\n";

    private static string Negotiated(string body) =>
        "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "
        + Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture) + "\r\n\r\n" + body;

    private string NextNegotiateBody() => negotiateBodies[Math.Min(Interlocked.Increment(ref negotiations), negotiateBodies.Count) - 1];

    private async Task PlayAsync(ReferenceConnection connection, SignalRReferenceReader reader, CancellationToken cancellationToken)
    {
        bool closeSent = false;
        foreach (SignalRHubStep step in script)
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
