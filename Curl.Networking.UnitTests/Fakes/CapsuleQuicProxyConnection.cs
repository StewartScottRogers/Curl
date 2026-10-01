using System.Net;
using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Quic;

namespace Curl.Networking.Fakes;

/// <summary>
/// A proxy connection that answers a CONNECT-UDP request with <c>reply</c> and then, when given a
/// <see cref="QuicTestServer" />, plays the far end of the tunnel: every <c>DATAGRAM</c> capsule
/// written after the request's head goes to the server, and each datagram it answers with is
/// read back as a <c>DATAGRAM</c> capsule. Without a server the connection reads its end once
/// the reply is read, as a proxy that closes the tunnel.
/// </summary>
/// <param name="reply">The proxy's reply head, sent before anything else.</param>
/// <param name="server">The QUIC server inside the tunnel, or <see langword="null" /> for none.</param>
internal sealed class CapsuleQuicProxyConnection(string reply, QuicTestServer? server) : IConnection
{
    private readonly Queue<byte> _toRead = new(Encoding.Latin1.GetBytes(reply));
    private readonly List<byte> _capsules = [];
    private readonly SemaphoreSlim _readable = new(0);

    /// <summary>Gets every byte written, the request's head first.</summary>
    public List<byte> Written { get; } = [];

    /// <summary>Gets the number of <c>DATAGRAM</c> capsules the server received.</summary>
    public int CapsulesReceived { get; private set; }

    /// <summary>Gets a value indicating whether the connection was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => false;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => null;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => new IPEndPoint(IPAddress.Loopback, 50000);

    /// <summary>Gets the request's head as written, up to and including its empty line.</summary>
    public string RequestHead
    {
        get
        {
            var text = Encoding.Latin1.GetString([.. Written]);
            return text[..(text.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)];
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_toRead)
            {
                if (_toRead.Count > 0)
                {
                    var count = Math.Min(buffer.Length, _toRead.Count);
                    for (var index = 0; index < count; index++)
                    {
                        buffer.Span[index] = _toRead.Dequeue();
                    }

                    return count;
                }

                if (server is null)
                {
                    return 0;
                }
            }

            await _readable.WaitAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        lock (_toRead)
        {
            var headWritten = Encoding.Latin1.GetString([.. Written]).Contains("\r\n\r\n", StringComparison.Ordinal);
            Written.AddRange(buffer.ToArray());
            if (headWritten && server is not null)
            {
                _capsules.AddRange(buffer.ToArray());
                AnswerWholeCapsules();
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    // Hands each whole capsule's datagram to the server and queues its answers as capsules.
    private void AnswerWholeCapsules()
    {
        var bytes = _capsules.ToArray();
        QuicVariableLengthInteger.TryRead(bytes, out _, out var typeBytes);
        QuicVariableLengthInteger.TryRead(bytes.AsSpan(typeBytes), out var length, out var lengthBytes);
        var end = typeBytes + lengthBytes + (int)length;
        if (bytes.Length < end)
        {
            return;
        }

        CapsulesReceived++;
        foreach (var answer in server!.Receive(bytes[(typeBytes + lengthBytes + 1)..end]))
        {
            foreach (var answerByte in CapsuleDatagramChannel.BuildCapsule(answer))
            {
                _toRead.Enqueue(answerByte);
            }
        }

        _capsules.RemoveRange(0, end);
        _readable.Release();
        if (_capsules.Count > 0)
        {
            AnswerWholeCapsules();
        }
    }
}
