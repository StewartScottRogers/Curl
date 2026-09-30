using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The secure <see cref="IConnection" /> <see cref="SslStreamTlsProvider" /> returns: reads
/// and writes go through the authenticated <see cref="SslStream" />, which runs over the
/// plaintext connection this one owns, as it owns the client certificate presented.
/// A read returns 0 at the server's <c>close_notify</c>, and fails with
/// <see cref="MissingCloseNotifyException" /> when the connection ends without one, as every
/// curl build fails it (ADR-0221). <see cref="SslStream" /> returns 0 for both, so the
/// transport under it says which: only a bare end reads the transport to its end.
/// </summary>
/// <param name="sslStream">The authenticated stream, left open on its inner stream.</param>
/// <param name="transport">The stream <paramref name="sslStream" /> runs over.</param>
/// <param name="plaintext">The connection underneath, disposed with this one.</param>
/// <param name="clientCertificate">
/// The <c>--cert</c> certificate presented, disposed with this one so a key the platform
/// stored for it is removed; <see langword="null" /> when none was.
/// </param>
/// <param name="missingCloseNotifyMessage">The message a read fails with when <c>close_notify</c> never came.</param>
internal sealed class SslStreamConnection(
    SslStream sslStream,
    ConnectionStream transport,
    IConnection plaintext,
    X509Certificate2? clientCertificate,
    string missingCloseNotifyMessage) : IConnection
{
    /// <summary>Gets <see langword="true" />: traffic is encrypted.</summary>
    public bool IsSecure => true;

    /// <summary>Gets the plaintext connection's remote endpoint.</summary>
    public EndPoint? RemoteEndPoint => plaintext.RemoteEndPoint;

    /// <summary>Gets the plaintext connection's local endpoint.</summary>
    public EndPoint? LocalEndPoint => plaintext.LocalEndPoint;

    /// <inheritdoc />
    /// <exception cref="MissingCloseNotifyException">
    /// The connection ended without <c>close_notify</c>, at a record boundary (where
    /// <see cref="SslStream" /> returns 0) or inside a record (where it throws).
    /// </exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int read;
        try
        {
            read = await sslStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) when (transport.TransportEnded)
        {
            throw new MissingCloseNotifyException(missingCloseNotifyMessage);
        }

        if (read == 0 && !buffer.IsEmpty && transport.TransportEnded)
        {
            throw new MissingCloseNotifyException(missingCloseNotifyMessage);
        }

        return read;
    }

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
        clientCertificate?.Dispose();
    }
}
