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
/// <param name="clearsTls">
/// Whether <see cref="ClearTlsAsync" /> hands back <paramref name="plaintext" /> as curl's
/// OpenSSL build clears TLS, or, <see langword="false" />, sends <c>close_notify</c> and fails
/// as curl 8.21.0's Schannel build does (ADR-0280).
/// </param>
internal sealed class SslStreamConnection(
    SslStream sslStream,
    ConnectionStream transport,
    IConnection plaintext,
    X509Certificate2? clientCertificate,
    string missingCloseNotifyMessage,
    bool clearsTls) : IConnection
{
    /// <summary>
    /// Gets the detector that tells, at the first read returning plaintext, how many session
    /// ticket records came before it; <see langword="null" /> when none is watching (BL-1089).
    /// </summary>
    internal SessionTicketRecordDetector? TicketRecords { get; init; }

    /// <summary>
    /// Gets where each session ticket record <see cref="TicketRecords" /> counts is reported, as
    /// a received <c>NewSessionTicket</c> <see cref="TlsMessageEvent" />, the handshake's events.
    /// </summary>
    internal ITransferEvents TicketEvents { get; init; } = NoTransferEvents.Instance;

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

        TicketRecords?.ReportTicketRecords(read, TicketEvents);
        return read;
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        sslStream.WriteAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        new(sslStream.FlushAsync(cancellationToken));

    /// <inheritdoc />
    /// <remarks>
    /// Measured with <c>--ftp-ssl-ccc</c> (BL-636): curl's OpenSSL build sends
    /// <c>close_notify</c> first only in active mode, and in either mode goes on in plain text
    /// once the server's has arrived; curl 8.21.0's Schannel build sends <c>close_notify</c> and
    /// fails, whichever side goes first.
    /// </remarks>
    public async ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken)
    {
        try
        {
            if (sendCloseNotifyFirst || !clearsTls)
            {
                await sslStream.ShutdownAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return clearsTls && await ReadsCloseNotifyAsync(cancellationToken).ConfigureAwait(false) ? plaintext : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await sslStream.DisposeAsync().ConfigureAwait(false);
        await plaintext.DisposeAsync().ConfigureAwait(false);
        clientCertificate?.Dispose();
    }

    // The server's close_notify reads as 0 with the transport still open; data or a bare end does not.
    private async ValueTask<bool> ReadsCloseNotifyAsync(CancellationToken cancellationToken) =>
        await sslStream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) == 0 && !transport.TransportEnded;
}
