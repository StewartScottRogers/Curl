using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;
using Curl.Quic;

namespace Curl.Networking;

/// <summary>
/// Carries UDP datagrams over a CONNECT-UDP tunnel (RFC 9298) as HTTP Datagram capsules
/// (RFC 9297), the way curl 8.22.0 runs QUIC through an HTTP proxy (BL-942): each datagram sent
/// is one <c>DATAGRAM</c> capsule - type 0, its length, context ID 0, the datagram - and each
/// <c>DATAGRAM</c> capsule received with context ID 0 is one datagram; every other capsule is
/// skipped.
/// </summary>
/// <remarks>
/// The tunnel has one peer, so every datagram is sent to it whatever destination is given and
/// arrives from <see cref="ServerEndPoint" />. Sends are serialised, so two capsules never
/// interleave on the stream. A tunnel the proxy closes fails the receive with a
/// <see cref="SocketException" /> for <see cref="SocketError.ConnectionReset" />, as a UDP
/// socket whose peer is gone fails it. Disposing the channel disposes the connection.
/// </remarks>
/// <param name="connection">The proxy connection the tunnel was opened on.</param>
/// <param name="serverEndPoint">The end point datagrams are reported to come from: the proxy's.</param>
internal sealed class CapsuleDatagramChannel(IConnection connection, EndPoint serverEndPoint) : IDatagramChannel
{
    /// <summary>The capsule type of an HTTP Datagram (RFC 9297 section 3.5).</summary>
    internal const ulong DatagramCapsuleType = 0;

    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly byte[] _oneByte = new byte[1];
    private int _disposed;

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => connection.LocalEndPoint;

    /// <summary>
    /// Builds the <c>DATAGRAM</c> capsule that carries <paramref name="datagram" />.
    /// </summary>
    /// <param name="datagram">The UDP payload.</param>
    /// <returns>Type 0, the length of context ID and payload, context ID 0, then the payload.</returns>
    internal static byte[] BuildCapsule(ReadOnlySpan<byte> datagram)
    {
        var length = (ulong)datagram.Length + 1;
        var lengthBytes = QuicVariableLengthInteger.GetEncodedLength(length);
        var capsule = new byte[1 + lengthBytes + 1 + datagram.Length];
        capsule[0] = (byte)DatagramCapsuleType;
        QuicVariableLengthInteger.Write(length, capsule.AsSpan(1));
        capsule[1 + lengthBytes] = 0;
        datagram.CopyTo(capsule.AsSpan(2 + lengthBytes));
        return capsule;
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)
    {
        var capsule = BuildCapsule(datagram.Span);
        await _sending.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.WriteAsync(capsule, cancellationToken).ConfigureAwait(false);
            await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sending.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>A datagram longer than <paramref name="buffer" /> is cut to it, as a UDP receive cuts it.</remarks>
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var type = await ReadVariableLengthIntegerAsync(cancellationToken).ConfigureAwait(false);
            var length = await ReadVariableLengthIntegerAsync(cancellationToken).ConfigureAwait(false);
            if (type != DatagramCapsuleType || length == 0)
            {
                await SkipAsync(length, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var contextIdBytes = await PeekVariableLengthIntegerLengthAsync(cancellationToken).ConfigureAwait(false);
            var contextId = await ReadVariableLengthIntegerRestAsync(contextIdBytes, cancellationToken).ConfigureAwait(false);
            var payloadLength = length - (ulong)contextIdBytes;
            if (contextId != 0)
            {
                await SkipAsync(payloadLength, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var kept = (int)Math.Min(payloadLength, (ulong)buffer.Length);
            await ReadExactlyAsync(buffer[..kept], cancellationToken).ConfigureAwait(false);
            await SkipAsync(payloadLength - (ulong)kept, cancellationToken).ConfigureAwait(false);
            return new DatagramReceived(kept, ServerEndPoint);
        }
    }

    /// <inheritdoc />
    /// <remarks>A second dispose does nothing, so a failed handshake and its dialer may both dispose the channel.</remarks>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            _sending.Dispose();
        }
    }

    // A variable-length integer (RFC 9000 section 16): its first byte's top two bits give its length.
    private async ValueTask<ulong> ReadVariableLengthIntegerAsync(CancellationToken cancellationToken)
    {
        var length = await PeekVariableLengthIntegerLengthAsync(cancellationToken).ConfigureAwait(false);
        return await ReadVariableLengthIntegerRestAsync(length, cancellationToken).ConfigureAwait(false);
    }

    // Reads the integer's first byte into _oneByte and gives the integer's whole length.
    private async ValueTask<int> PeekVariableLengthIntegerLengthAsync(CancellationToken cancellationToken)
    {
        await ReadExactlyAsync(_oneByte, cancellationToken).ConfigureAwait(false);
        return 1 << (_oneByte[0] >> 6);
    }

    // The integer whose first byte is in _oneByte and whose remaining bytes follow on the stream.
    private async ValueTask<ulong> ReadVariableLengthIntegerRestAsync(int length, CancellationToken cancellationToken)
    {
        var encoded = new byte[length];
        encoded[0] = _oneByte[0];
        await ReadExactlyAsync(encoded.AsMemory(1), cancellationToken).ConfigureAwait(false);
        QuicVariableLengthInteger.TryRead(encoded, out var value, out _);
        return value;
    }

    private async ValueTask SkipAsync(ulong count, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        for (var left = count; left > 0;)
        {
            var chunk = (int)Math.Min(left, (ulong)buffer.Length);
            await ReadExactlyAsync(buffer.AsMemory(0, chunk), cancellationToken).ConfigureAwait(false);
            left -= (ulong)chunk;
        }
    }

    private async ValueTask ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        while (!destination.IsEmpty)
        {
            var read = await connection.ReadAsync(destination, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }

            destination = destination[read..];
        }
    }
}
