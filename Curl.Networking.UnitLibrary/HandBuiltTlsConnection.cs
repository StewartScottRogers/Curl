using System.Net;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The <see cref="IConnection" /> <see cref="HandBuiltTlsProvider" /> returns: application data
/// through the hand-built client's stream, the end points of the plaintext connection under it.
/// A read returns 0 at the server's <c>close_notify</c>, and fails with
/// <see cref="MissingCloseNotifyException" /> when the connection ends without one, as every
/// curl build fails it (ADR-0221).
/// </summary>
/// <param name="tlsStream">
/// The hand-built client's connected stream, a <see cref="Tls13ClientStream" /> or a
/// <see cref="Tls12ClientStream" />.
/// </param>
/// <param name="plaintext">The connection the handshake ran over.</param>
/// <param name="clientCertificate">The <c>--cert</c> certificate, disposed with the connection.</param>
/// <param name="missingCloseNotifyMessage">The message a read fails with when <c>close_notify</c> never came.</param>
internal sealed class HandBuiltTlsConnection(
    Stream tlsStream,
    IConnection plaintext,
    X509Certificate2? clientCertificate,
    string missingCloseNotifyMessage) : IConnection
{
    public bool IsSecure => true;

    public EndPoint? RemoteEndPoint => plaintext.RemoteEndPoint;

    public EndPoint? LocalEndPoint => plaintext.LocalEndPoint;

    /// <exception cref="MissingCloseNotifyException">The connection ended without <c>close_notify</c>.</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await tlsStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0 && !buffer.IsEmpty && !CloseNotifyReceived(tlsStream))
        {
            throw new MissingCloseNotifyException(missingCloseNotifyMessage);
        }

        return read;
    }

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

    private static bool CloseNotifyReceived(Stream tlsStream) =>
        tlsStream is Tls13ClientStream tls13 ? tls13.CloseNotifyReceived : ((Tls12ClientStream)tlsStream).CloseNotifyReceived;
}
