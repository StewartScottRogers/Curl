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
/// curl build fails it (ADR-0221). <see cref="ClearTlsAsync" /> clears TLS for FTP's <c>CCC</c>
/// as <see cref="SslStreamConnection" /> does (ADR-0280).
/// </summary>
/// <param name="tlsStream">
/// The hand-built client's connected stream, a <see cref="Tls13ClientStream" /> or a
/// <see cref="Tls12ClientStream" />.
/// </param>
/// <param name="plaintext">The connection the handshake ran over.</param>
/// <param name="clientCertificate">The <c>--cert</c> certificate, disposed with the connection.</param>
/// <param name="missingCloseNotifyMessage">The message a read fails with when <c>close_notify</c> never came.</param>
/// <param name="clearsTls">
/// Whether <see cref="ClearTlsAsync" /> hands back <paramref name="plaintext" /> as curl's
/// OpenSSL build clears TLS, or, <see langword="false" />, sends <c>close_notify</c> and fails
/// as curl 8.21.0's Schannel build does (ADR-0280).
/// </param>
internal sealed class HandBuiltTlsConnection(
    Stream tlsStream,
    IConnection plaintext,
    X509Certificate2? clientCertificate,
    string missingCloseNotifyMessage,
    bool clearsTls) : IConnection
{
    private int _ticketsReported;

    /// <summary>
    /// Gets where each TLS 1.3 <c>NewSessionTicket</c> a read takes in is reported, as a received
    /// <c>NewSessionTicket</c> <see cref="TlsMessageEvent" />, or <see langword="null" /> to report
    /// none: the Schannel build reports them, as <see cref="SslStreamConnection" /> does (BL-1096).
    /// </summary>
    internal ITransferEvents? TicketEvents { get; init; }

    public bool IsSecure => true;

    public EndPoint? RemoteEndPoint => plaintext.RemoteEndPoint;

    public EndPoint? LocalEndPoint => plaintext.LocalEndPoint;

    public bool HasPeerClosed => plaintext.HasPeerClosed;

    /// <exception cref="MissingCloseNotifyException">The connection ended without <c>close_notify</c>.</exception>
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await tlsStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0 && !buffer.IsEmpty && !CloseNotifyReceived(tlsStream))
        {
            throw new MissingCloseNotifyException(missingCloseNotifyMessage);
        }

        ReportNewTickets();
        return read;
    }

    // The client stream takes each ticket in the clear, one per record from the servers curl
    // meets, so each ticket received since the last read is reported as one ticket record.
    private void ReportNewTickets()
    {
        if (TicketEvents is null || tlsStream is not Tls13ClientStream tls13)
        {
            return;
        }

        for (; _ticketsReported < tls13.Handshake.ReceivedTickets.Count; _ticketsReported++)
        {
            TicketEvents.ReportTlsMessage(SessionTicketRecordDetector.NewSessionTicketReceived);
        }
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        tlsStream.WriteAsync(buffer, cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(tlsStream.FlushAsync(cancellationToken));

    /// <inheritdoc />
    public async ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken)
    {
        try
        {
            if (sendCloseNotifyFirst || !clearsTls)
            {
                await ShutdownAsync(tlsStream, cancellationToken).ConfigureAwait(false);
            }

            return clearsTls && await ReadsCloseNotifyAsync(cancellationToken).ConfigureAwait(false) ? plaintext : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await tlsStream.DisposeAsync().ConfigureAwait(false);
        await plaintext.DisposeAsync().ConfigureAwait(false);
        clientCertificate?.Dispose();
    }

    // The server's close_notify reads as 0 and is recorded; data, or a bare end, is not. The
    // record layers read the transport a record at a time, so nothing after close_notify is lost.
    private async ValueTask<bool> ReadsCloseNotifyAsync(CancellationToken cancellationToken) =>
        await tlsStream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) == 0 && CloseNotifyReceived(tlsStream);

    private static Task ShutdownAsync(Stream tlsStream, CancellationToken cancellationToken) =>
        tlsStream is Tls13ClientStream tls13 ? tls13.ShutdownAsync(cancellationToken) : ((Tls12ClientStream)tlsStream).ShutdownAsync(cancellationToken);

    private static bool CloseNotifyReceived(Stream tlsStream) =>
        tlsStream is Tls13ClientStream tls13 ? tls13.CloseNotifyReceived : ((Tls12ClientStream)tlsStream).CloseNotifyReceived;
}
