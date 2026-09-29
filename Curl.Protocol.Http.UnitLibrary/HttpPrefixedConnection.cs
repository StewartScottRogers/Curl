using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Reads <paramref name="prefix" />, then from <paramref name="rest" />, and passes everything
/// else to <paramref name="rest" />: the connection an h2c upgrade continues on, whose first
/// HTTP/2 bytes may have arrived with the <c>101</c>'s head (<see cref="HttpH2cUpgradeConnection" />).
/// </summary>
/// <remarks>
/// Disposing it does nothing: <paramref name="rest" /> belongs to whoever opened it.
/// </remarks>
/// <param name="prefix">The bytes read first.</param>
/// <param name="rest">The connection read once <paramref name="prefix" /> is used up.</param>
internal sealed class HttpPrefixedConnection(ReadOnlyMemory<byte> prefix, IConnection rest) : IConnection
{
    private ReadOnlyMemory<byte> remainingPrefix = prefix;

    /// <inheritdoc />
    public bool IsSecure => rest.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => rest.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => rest.LocalEndPoint;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (remainingPrefix.IsEmpty)
        {
            return rest.ReadAsync(buffer, cancellationToken);
        }

        int length = Math.Min(buffer.Length, remainingPrefix.Length);
        remainingPrefix[..length].CopyTo(buffer);
        remainingPrefix = remainingPrefix[length..];
        return ValueTask.FromResult(length);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        rest.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => rest.FlushAsync(cancellationToken);

    /// <summary>
    /// Does nothing: the connection read after the prefix is disposed by whoever opened it.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
