using System.Net;
using Curl.Extension.Protocol.Coap.Dtls.Handshake;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Protocol.Coap.Secure;

/// <summary>
/// A datagram channel secured by DTLS 1.2 (RFC 7252 section 9.1): each datagram sent is one protected
/// application data record of the association, and each datagram received is the plaintext of the
/// server's next one, so the CoAP exchange, block-wise transfer and observation run over it unchanged.
/// Its opener still owns the UDP channel beneath it and disposes that.
/// </summary>
internal sealed class DtlsDatagramChannel : IDatagramChannel
{
    private readonly IDatagramChannel transport;

    private readonly DtlsClientSession session;

    private DtlsDatagramChannel(IDatagramChannel transport, DtlsClientSession session)
    {
        this.transport = transport;
        this.session = session;
    }

    /// <inheritdoc />
    public EndPoint ServerEndPoint => transport.ServerEndPoint;

    /// <summary>
    /// Runs the DTLS 1.2 handshake over <paramref name="transport" />, verifying the server's certificate
    /// for <paramref name="targetHost" /> through <paramref name="tlsProvider" /> (section 9.1.3.3), and
    /// gives the secured channel.
    /// </summary>
    /// <param name="transport">The UDP channel to the server.</param>
    /// <param name="tlsProvider">Verifies the server's certificate chain under curl's trust settings.</param>
    /// <param name="targetHost">The host the certificate must name.</param>
    /// <param name="timeProvider">Times the handshake's flight retransmissions.</param>
    /// <param name="cancellationToken">Stops the handshake.</param>
    /// <returns>The secured channel.</returns>
    /// <exception cref="Client.CoapException">The handshake failed; its exit code says why.</exception>
    public static async ValueTask<DtlsDatagramChannel> OpenAsync(IDatagramChannel transport, ITlsProvider tlsProvider, string targetHost, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        DtlsClientSession session = await new DtlsClientHandshake(transport, tlsProvider, targetHost, timeProvider).RunAsync(cancellationToken).ConfigureAwait(false);
        return new DtlsDatagramChannel(transport, session);
    }

    /// <inheritdoc />
    public ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken) =>
        session.SendAsync(datagram, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        byte[] plaintext = await session.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        plaintext.CopyTo(buffer);
        return new DatagramReceived(plaintext.Length, transport.ServerEndPoint);
    }

    /// <summary>Disposes the association's record keys, leaving the UDP channel to its opener.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        session.Dispose();
        return ValueTask.CompletedTask;
    }
}
