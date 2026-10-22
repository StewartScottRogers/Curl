using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;
using Curl.Quic;

namespace Curl.Networking;

/// <summary>
/// Carries UDP datagrams over a CONNECT-UDP tunnel (RFC 9298) as HTTP Datagram capsules
/// (RFC 9297), the way curl 8.22.0 runs QUIC through an HTTP proxy (BL-942): each datagram sent
/// is one <c>DATAGRAM</c> capsule - type 0, its length, context ID 0, the datagram - and each
/// capsule received is decoded by curl's rules (<see cref="DecodeDatagram" />), so only a
/// <c>DATAGRAM</c> capsule with context ID 0 is a datagram and any other capsule fails the
/// receive (ADR-0726).
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

    private const int FirstReceiveBufferLength = 4096;

    private readonly SemaphoreSlim _sending = new(1, 1);
    private byte[] _received = new byte[FirstReceiveBufferLength];
    private int _receivedCount;
    private int _disposed;

    /// <summary>What <see cref="DecodeDatagram" /> found: curl's <c>CURLcode</c> for the decode.</summary>
    internal enum DecodeStatus
    {
        /// <summary>A datagram was decoded (<c>CURLE_OK</c>).</summary>
        Decoded,

        /// <summary>The capsule is not all here yet (<c>CURLE_AGAIN</c>).</summary>
        NeedMoreBytes,

        /// <summary>The destination is empty (<c>CURLE_BAD_FUNCTION_ARGUMENT</c>).</summary>
        EmptyDestination,

        /// <summary>The capsule is one curl rejects (<c>CURLE_RECV_ERROR</c>).</summary>
        Malformed,
    }

    /// <inheritdoc />
    public EndPoint ServerEndPoint { get; } = serverEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => connection.LocalEndPoint;

    /// <summary>
    /// Builds the <c>DATAGRAM</c> capsule that carries <paramref name="datagram" />.
    /// </summary>
    /// <param name="datagram">The UDP payload.</param>
    /// <returns>Type 0, the length of context ID and payload, context ID 0, then the payload.</returns>
    internal static byte[] BuildCapsule(ReadOnlySpan<byte> datagram) => [.. BuildCapsuleHeader(datagram.Length), .. datagram];

    /// <summary>
    /// Builds the header of the <c>DATAGRAM</c> capsule that carries a payload of
    /// <paramref name="payloadLength" /> bytes: curl's <c>capsule_encap_udp_hdr</c>.
    /// </summary>
    /// <param name="payloadLength">The UDP payload's length.</param>
    /// <returns>Type 0, <paramref name="payloadLength" /> + 1 as a variable-length integer, then context ID 0.</returns>
    internal static byte[] BuildCapsuleHeader(int payloadLength)
    {
        var length = (ulong)payloadLength + 1;
        var header = new byte[2 + QuicVariableLengthInteger.GetEncodedLength(length)];
        header[0] = (byte)DatagramCapsuleType;
        QuicVariableLengthInteger.Write(length, header.AsSpan(1));
        return header;
    }

    /// <summary>
    /// Decodes the <c>DATAGRAM</c> capsule at the start of <paramref name="received" /> into
    /// <paramref name="destination" /> by curl's rules: <c>Curl_capsule_process_udp_raw</c>.
    /// </summary>
    /// <remarks>
    /// Another capsule type, a context ID other than 0, a capsule length of 0 or one past
    /// <see cref="int.MaxValue" /> is <see cref="DecodeStatus.Malformed" />, and so is a payload
    /// longer than <paramref name="destination" />, which is consumed whole and discarded. The
    /// context ID is one byte, as curl reads it.
    /// </remarks>
    /// <param name="received">The bytes received and not yet consumed.</param>
    /// <param name="destination">Where the payload goes.</param>
    /// <param name="consumed">How many bytes of <paramref name="received" /> to drop: the capsule, or the bytes curl skips on an error.</param>
    /// <param name="payloadLength">The payload's length when <see cref="DecodeStatus.Decoded" />, else 0.</param>
    /// <returns>What the decode found.</returns>
    internal static DecodeStatus DecodeDatagram(ReadOnlySpan<byte> received, Span<byte> destination, out int consumed, out int payloadLength)
    {
        payloadLength = 0;
        if (destination.IsEmpty)
        {
            consumed = 0;
            return DecodeStatus.EmptyDestination;
        }

        var status = DecodeHeader(received, out consumed, out var payloadStart, out var payload);
        if (status != DecodeStatus.Decoded)
        {
            return status;
        }

        if (received.Length - payloadStart < payload)
        {
            return DecodeStatus.NeedMoreBytes;
        }

        consumed = payloadStart + payload;
        if (payload > destination.Length)
        {
            return DecodeStatus.Malformed;
        }

        received.Slice(payloadStart, payload).CopyTo(destination);
        payloadLength = payload;
        return DecodeStatus.Decoded;
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
    /// <remarks>
    /// An empty <paramref name="buffer" /> throws <see cref="ArgumentException" />, and a capsule
    /// curl rejects (<see cref="DecodeDatagram" />), a datagram longer than
    /// <paramref name="buffer" /> included, fails the receive with a
    /// <see cref="SocketException" /> for <see cref="SocketError.ConnectionAborted" />, where
    /// curl's receive fails with <c>CURLE_RECV_ERROR</c>.
    /// </remarks>
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        while (true)
        {
            var status = TakeDatagram(buffer.Span, out var payloadLength);
            if (status != DecodeStatus.NeedMoreBytes)
            {
                return Received(status, payloadLength);
            }

            await ReceiveMoreAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // The datagram a finished decode gives, or the exception its failure throws.
    private DatagramReceived Received(DecodeStatus status, int payloadLength) => status switch
    {
        DecodeStatus.Decoded => new DatagramReceived(payloadLength, ServerEndPoint),
        DecodeStatus.EmptyDestination => throw new ArgumentException("The receive buffer is empty.", "buffer"),
        _ => throw new SocketException((int)SocketError.ConnectionAborted),
    };

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

    // The capsule's type, length and context ID: Decoded, consuming nothing, when all three are good.
    private static DecodeStatus DecodeHeader(ReadOnlySpan<byte> received, out int consumed, out int payloadStart, out int payloadLength)
    {
        consumed = 0;
        payloadStart = 0;
        payloadLength = 0;
        if (received.IsEmpty)
        {
            return DecodeStatus.NeedMoreBytes;
        }

        if (received[0] != DatagramCapsuleType)
        {
            consumed = 1;
            return DecodeStatus.Malformed;
        }

        if (!QuicVariableLengthInteger.TryRead(received[1..], out var length, out var lengthBytes) || received.Length <= 1 + lengthBytes)
        {
            return DecodeStatus.NeedMoreBytes;
        }

        payloadStart = 2 + lengthBytes;
        return DecodeContextIdAndLength(received[1 + lengthBytes], length, payloadStart, out consumed, out payloadLength);
    }

    // The context ID must be 0 and the length neither 0 nor past int.MaxValue; curl skips past the context ID for the first two.
    private static DecodeStatus DecodeContextIdAndLength(byte contextId, ulong length, int payloadStart, out int consumed, out int payloadLength)
    {
        consumed = 0;
        payloadLength = 0;
        if (contextId != 0 || length == 0)
        {
            consumed = payloadStart;
            return DecodeStatus.Malformed;
        }

        if (length - 1 > int.MaxValue)
        {
            return DecodeStatus.Malformed;
        }

        payloadLength = (int)(length - 1);
        return DecodeStatus.Decoded;
    }

    // Decodes from the bytes received so far and drops what the decode consumed.
    private DecodeStatus TakeDatagram(Span<byte> destination, out int payloadLength)
    {
        var status = DecodeDatagram(_received.AsSpan(0, _receivedCount), destination, out var consumed, out payloadLength);
        _received.AsSpan(consumed, _receivedCount - consumed).CopyTo(_received);
        _receivedCount -= consumed;
        return status;
    }

    // Reads more of the stream after the bytes received so far, doubling the buffer when it is full.
    private async ValueTask ReceiveMoreAsync(CancellationToken cancellationToken)
    {
        if (_receivedCount == _received.Length)
        {
            Array.Resize(ref _received, _received.Length * 2);
        }

        var read = await connection.ReadAsync(_received.AsMemory(_receivedCount), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            throw new SocketException((int)SocketError.ConnectionReset);
        }

        _receivedCount += read;
    }
}
