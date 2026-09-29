using System.Net;
using System.Net.Sockets;
using Curl.Protocol.Abstractions;

namespace Curl.Networking.Fakes;

/// <summary>
/// An <see cref="IConnection" /> that passes everything to the one it wraps until
/// <see cref="Reset" /> is called; from then on every read fails as
/// <see cref="NetworkStream" /> reports a reset, so a TLS handshake can complete before the
/// transport fails.
/// </summary>
/// <param name="inner">The connection carrying the bytes until the reset.</param>
public sealed class ResetOnDemandConnection(IConnection inner) : IConnection
{
    private bool _reset;

    /// <inheritdoc />
    public bool IsSecure => inner.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    /// <summary>Makes every later read fail with a reset.</summary>
    public void Reset() => _reset = true;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        _reset
            ? ValueTask.FromException<int>(
                new IOException("Unable to read data from the transport connection.", new SocketException((int)SocketError.ConnectionReset)))
            : inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        inner.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
