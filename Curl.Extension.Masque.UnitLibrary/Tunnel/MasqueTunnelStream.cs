using System.Net.Sockets;
using System.Text;
using Curl.Extension.Masque.Wire;
using Curl.HttpDatagram;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Masque.Tunnel;

/// <summary>
/// The byte stream of one MASQUE tunnel over curl's <see cref="IConnection" />: the upgrade
/// reply head, then capsules (RFC 9297 section 3.2) both ways. Disposing it disposes the connection.
/// </summary>
/// <param name="connection">The connection to the proxy.</param>
internal sealed class MasqueTunnelStream(IConnection connection) : IAsyncDisposable
{
    /// <summary>The most bytes the stream buffers while waiting for the end of a reply head or capsule.</summary>
    public const int MaximumBufferedLength = 1024 * 1024;

    private static readonly byte[] HeadEnd = "\r\n\r\n"u8.ToArray();

    private byte[] _received = new byte[4096];
    private int _receivedCount;

    /// <summary>Writes bytes to the proxy and flushes them.</summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are flushed.</returns>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await connection.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await connection.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes one capsule to the proxy and flushes it.</summary>
    /// <param name="capsule">The capsule to send.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the capsule is flushed.</returns>
    public ValueTask WriteCapsuleAsync(Capsule capsule, CancellationToken cancellationToken) =>
        WriteAsync(CapsuleCodec.Encode(capsule), cancellationToken);

    /// <summary>Reads the upgrade reply's status line and header lines, up to the empty line that ends them.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The head without its final empty line, or <see langword="null" /> when the proxy closed the stream first.</returns>
    /// <exception cref="MasqueException">The head runs past <see cref="MaximumBufferedLength" /> bytes.</exception>
    public async ValueTask<string?> ReadHeadAsync(CancellationToken cancellationToken)
    {
        int end;
        while ((end = _received.AsSpan(0, _receivedCount).IndexOf(HeadEnd)) < 0)
        {
            if (!await ReceiveMoreAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }

        var head = Encoding.Latin1.GetString(_received, 0, end);
        Consume(end + HeadEnd.Length);
        return head;
    }

    /// <summary>Reads the next whole capsule, of any type.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The capsule, or <see langword="null" /> when the proxy closed the stream first.</returns>
    /// <exception cref="MasqueException">The capsule runs past <see cref="MaximumBufferedLength" /> bytes.</exception>
    public async ValueTask<Capsule?> ReadCapsuleAsync(CancellationToken cancellationToken)
    {
        Capsule? capsule;
        int consumed;
        while (!CapsuleCodec.TryDecode(_received.AsSpan(0, _receivedCount), out capsule, out consumed))
        {
            if (!await ReceiveMoreAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }

        Consume(consumed);
        return capsule;
    }

    /// <summary>
    /// Reads the content of the next DATAGRAM capsule with Context ID 0, dropping capsules of
    /// other types (RFC 9297 section 3.2) and datagrams of other Context IDs (RFC 9298 section 5).
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The UDP payload or IP packet the datagram carries.</returns>
    /// <exception cref="SocketException">The proxy closed the tunnel: <see cref="SocketError.ConnectionReset" />, as a reset UDP socket fails.</exception>
    public async ValueTask<ReadOnlyMemory<byte>> ReadDatagramContentAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var capsule = await ReadCapsuleAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new SocketException((int)SocketError.ConnectionReset);
            if (capsule.Type == CapsuleType.Datagram && MasqueDatagramPayload.TryUnwrap(capsule.Value, out var content))
            {
                return content;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();

    // Reads more of the stream after the bytes received so far, doubling the buffer when it is full.
    private async ValueTask<bool> ReceiveMoreAsync(CancellationToken cancellationToken)
    {
        if (_receivedCount == _received.Length)
        {
            if (_received.Length >= MaximumBufferedLength)
            {
                throw new MasqueException($"MASQUE proxy sent more than {MaximumBufferedLength} bytes without ending a reply head or capsule");
            }

            Array.Resize(ref _received, _received.Length * 2);
        }

        var read = await connection.ReadAsync(_received.AsMemory(_receivedCount), cancellationToken).ConfigureAwait(false);
        _receivedCount += read;
        return read > 0;
    }

    private void Consume(int count)
    {
        _received.AsSpan(count, _receivedCount - count).CopyTo(_received);
        _receivedCount -= count;
    }
}
