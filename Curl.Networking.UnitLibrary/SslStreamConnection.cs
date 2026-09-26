using System.Net;
using System.Net.Security;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The secure <see cref="IConnection" /> <see cref="SslStreamTlsProvider" /> returns: reads
/// and writes go through the authenticated <see cref="SslStream" />, which runs over the
/// plaintext connection this one owns.
/// </summary>
/// <param name="sslStream">The authenticated stream, left open on its inner stream.</param>
/// <param name="plaintext">The connection underneath, disposed with this one.</param>
internal sealed class SslStreamConnection(SslStream sslStream, IConnection plaintext) : IConnection
{
    /// <summary>Gets <see langword="true" />: traffic is encrypted.</summary>
    public bool IsSecure => true;

    /// <summary>Gets the plaintext connection's remote endpoint.</summary>
    public EndPoint? RemoteEndPoint => plaintext.RemoteEndPoint;

    /// <inheritdoc />
    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        sslStream.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        sslStream.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(sslStream.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await sslStream.DisposeAsync().ConfigureAwait(false);
        await plaintext.DisposeAsync().ConfigureAwait(false);
    }
}
