using System.Net;
using System.Runtime.CompilerServices;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Stands in for the connection a response is read from, and records when its first byte
/// arrived: the source of <c>%{time_starttransfer}</c> (<see cref="TransferTimings.FirstByteReceived" />).
/// </summary>
/// <remarks>
/// curl 8.21.0 takes that moment when the first byte of any response arrives, a
/// <c>100 Continue</c> included, and leaves it at <c>0</c> when none does: a server that
/// closes without a reply prints <c>time_starttransfer</c> <c>0.000000</c> with exit 52
/// (measured, BL-287 Notes). Disposing this does nothing: the connection it wraps belongs to
/// the handler.
/// </remarks>
/// <param name="connection">The connection the response is read from.</param>
/// <param name="timeProvider">The clock the moment is taken on.</param>
internal sealed class HttpFirstByteTimingConnection(IConnection connection, TimeProvider timeProvider) : IConnection
{
    /// <summary>
    /// Gets the <see cref="TimeProvider.GetTimestamp" /> value taken when the first read
    /// returned bytes, or <see langword="null" /> until one has.
    /// </summary>
    internal long? FirstByteReceived { get; private set; }

    /// <inheritdoc />
    public bool IsSecure => connection.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => connection.RemoteEndPoint;

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read = await connection.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read > 0)
        {
            FirstByteReceived ??= timeProvider.GetTimestamp();
        }

        return read;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        connection.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => connection.FlushAsync(cancellationToken);

    /// <summary>
    /// Does nothing: the wrapped connection is disposed by whoever opened it.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
