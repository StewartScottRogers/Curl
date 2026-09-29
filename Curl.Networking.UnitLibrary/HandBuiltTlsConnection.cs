using System.Net;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The <see cref="IConnection" /> <see cref="HandBuiltTlsProvider" /> returns: application data
/// through the hand-built client's stream, the end points of the plaintext connection under it.
/// Like <see cref="SslStreamConnection" />, a read returns 0 at the server's
/// <c>close_notify</c> and at a bare end of the connection alike (ADR-0157).
/// </summary>
/// <param name="tlsStream">The hand-built client's connected stream.</param>
/// <param name="plaintext">The connection the handshake ran over.</param>
/// <param name="clientCertificate">The <c>--cert</c> certificate, disposed with the connection.</param>
internal sealed class HandBuiltTlsConnection(Stream tlsStream, IConnection plaintext, X509Certificate2? clientCertificate) : IConnection
{
    public bool IsSecure => true;

    public EndPoint? RemoteEndPoint => plaintext.RemoteEndPoint;

    public EndPoint? LocalEndPoint => plaintext.LocalEndPoint;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        tlsStream.ReadAsync(buffer, cancellationToken);

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        tlsStream.WriteAsync(buffer, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(tlsStream.FlushAsync(cancellationToken));

    public async ValueTask DisposeAsync()
    {
        await tlsStream.DisposeAsync().ConfigureAwait(false);
        await plaintext.DisposeAsync().ConfigureAwait(false);
        clientCertificate?.Dispose();
    }
}
