using System.Net;
using System.Text;
using Curl.Protocol.Abstractions;
using Curl.WebSocket;

namespace Curl.Extension.SocketIo;

/// <summary>
/// A connection that gives each scripted chunk to one read, then the end of the stream, and records
/// what is written; with <see cref="ZeroRandomSource" /> every client frame's mask is zero, so its
/// payload is readable as written.
/// </summary>
internal sealed class ScriptedConnection(params byte[][] chunks) : IConnection
{
    private readonly Queue<byte[]> reads = new(chunks);

    public bool IsSecure => false;

    public EndPoint? RemoteEndPoint => null;

    public List<byte[]> Writes { get; } = [];

    public bool IsDisposed { get; private set; }

    /// <summary>Gives an unmasked server frame with FIN set.</summary>
    public static byte[] ServerFrame(WsOpcode opcode, string payload, bool isFinal = true)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        return [(byte)((isFinal ? 0x80 : 0) | (int)opcode), (byte)bytes.Length, .. bytes];
    }

    /// <summary>Gives the 101 reply head for the key <see cref="ZeroRandomSource" /> makes.</summary>
    public static byte[] SwitchingProtocols() => Encoding.ASCII.GetBytes(
        "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
        $"Sec-WebSocket-Accept: {SocketIoUpgrade.AcceptFor(ZeroRandomSource.Key)}\r\n\r\n");

    /// <summary>Gives each written client frame's opcode and payload text, the upgrade request left out.</summary>
    public string[] SentFrames() =>
        [.. Writes.Where(write => write.Length > 0 && write[0] != (byte)'G').Select(write => $"{write[0] & 0x0F}:{Encoding.UTF8.GetString(write.AsSpan(6))}")];

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (!reads.TryDequeue(out byte[]? chunk))
        {
            return ValueTask.FromResult(0);
        }

        chunk.CopyTo(buffer);
        return ValueTask.FromResult(chunk.Length);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Writes.Add(buffer.ToArray());
        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
