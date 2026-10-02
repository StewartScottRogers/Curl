using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// A TLS connection whose handshake is deferred to its first write, as curl defers it for
/// <c>--tls-earlydata</c> (BL-1105): that write's bytes go to <paramref name="handshake" />,
/// which sends them as 0-RTT early data on the resumed session (or after the handshake when
/// the server refuses them) and returns the connected TLS connection every later call goes to.
/// A read or flush before any write runs the handshake with no early data.
/// </summary>
/// <param name="plaintext">The connection the handshake runs over, disposed when no handshake ran.</param>
/// <param name="handshake">Runs the handshake with the given early data and returns the TLS connection.</param>
internal sealed class EarlyDataTlsConnection(
    IConnection plaintext,
    Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask<IConnection>> handshake) : IConnection
{
    private IConnection? _connected;

    public bool IsSecure => true;

    public EndPoint? RemoteEndPoint => plaintext.RemoteEndPoint;

    public EndPoint? LocalEndPoint => plaintext.LocalEndPoint;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        await (await ConnectedAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false)).ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_connected is not null)
        {
            await _connected.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            return;
        }

        // The handshake sends the bytes itself, as early data or after it completes.
        await ConnectedAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken) =>
        await (await ConnectedAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false)).FlushAsync(cancellationToken).ConfigureAwait(false);

    public ValueTask DisposeAsync() => (_connected ?? plaintext).DisposeAsync();

    private async ValueTask<IConnection> ConnectedAsync(ReadOnlyMemory<byte> earlyData, CancellationToken cancellationToken) =>
        _connected ??= await handshake(earlyData, cancellationToken).ConfigureAwait(false);
}
