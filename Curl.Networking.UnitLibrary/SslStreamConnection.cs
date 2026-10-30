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
    // Set once close_notify has been sent or TLS cleared, so disposing sends no second one.
    private bool _tlsEnded;

    /// <summary>Gets whether the curl build being matched is the Schannel one, which words a read cut by an alert its own way.</summary>
    internal bool MatchesSchannelBuild { get; init; }

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

    /// <summary>Gets the plaintext connection's socket handle.</summary>
    public nint? SocketHandle => plaintext.SocketHandle;

    /// <summary>Asks the plaintext connection TLS runs over whether the peer has closed it.</summary>
    public bool HasPeerClosed => plaintext.HasPeerClosed;

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
        catch (IOException failure) when (
            TlsFailureMessages.AlertReadFailure(failure, MatchesSchannelBuild, clientCertificate is not null) is not null)
        {
            // MissingCloseNotifyException is the exception whose message the HTTP handler prints as it stands.
            throw new MissingCloseNotifyException(
                TlsFailureMessages.AlertReadFailure(failure, MatchesSchannelBuild, clientCertificate is not null)!);
        }

        if (read == 0 && !buffer.IsEmpty && transport.TransportEnded)
        {
            throw new MissingCloseNotifyException(missingCloseNotifyMessage);
        }

        TicketRecords?.ReportTicketRecords(read, TicketEvents);
        ReportShutdownAfterCloseNotify(read, buffer.IsEmpty, TicketRecords, TicketEvents);
        return read;
    }

    /// <summary>
    /// Reports, for a read that returned 0 at <c>close_notify</c> after
    /// <paramref name="ticketRecords" /> found it already received, curl's Schannel build's
    /// <c>server indicated shutdown in a prior call</c> line and the 0 bytes it passes on
    /// (measured with curl 8.22.0 against <c>openssl s_server -WWW</c>, BL-4067).
    /// </summary>
    /// <param name="read">The bytes the read returned.</param>
    /// <param name="bufferIsEmpty">Whether the read asked for no bytes.</param>
    /// <param name="ticketRecords">The detector watching the records, or <see langword="null" />.</param>
    /// <param name="events">Where the line and the 0 bytes are reported.</param>
    internal static void ReportShutdownAfterCloseNotify(int read, bool bufferIsEmpty, SessionTicketRecordDetector? ticketRecords, ITransferEvents events)
    {
        if (read == 0 && !bufferIsEmpty && ticketRecords is { CloseNotifyFollows: true })
        {
            events.ReportInfo("schannel: server indicated shutdown in a prior call");
            events.ReportDataReceived([]);
        }
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
        _tlsEnded = true;
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
    /// <remarks>Sends <c>close_notify</c>, then ends the TCP send side under it.</remarks>
    public async ValueTask ShutDownSendingAsync(CancellationToken cancellationToken)
    {
        _tlsEnded = true;
        await sslStream.ShutdownAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await plaintext.ShutDownSendingAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends <c>close_notify</c> first, as curl 8.18.0's OpenSSL build and curl 8.22.0's Schannel
    /// build both do when they close a connection (BL-3984, ADR-0959), unless TLS has already
    /// ended or the server has closed the transport; a failure to send it is ignored, as curl's is.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        await SendCloseNotifyAsync().ConfigureAwait(false);
        try
        {
            await sslStream.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The transport is closed below whatever the TLS stream's own disposal throws.
        }

        await plaintext.DisposeAsync().ConfigureAwait(false);
        clientCertificate?.Dispose();
    }

    /// <summary>Gets how long disposing waits for <c>close_notify</c> to be sent before it gives up, as curl's non-blocking <c>SSL_shutdown</c> does.</summary>
    internal TimeSpan CloseNotifyTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets the clock <see cref="CloseNotifyTimeout" /> runs on.</summary>
    internal TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    private async ValueTask SendCloseNotifyAsync()
    {
        if (_tlsEnded || transport.TransportEnded)
        {
            return;
        }

        _tlsEnded = true;
        try
        {
            using CancellationTokenSource timeout = new(CloseNotifyTimeout, TimeProvider);
            await sslStream.ShutdownAsync().WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The transfer is over either way; curl ignores a close_notify it cannot send too.
        }
    }

    // The server's close_notify reads as 0 with the transport still open; data or a bare end does not.
    private async ValueTask<bool> ReadsCloseNotifyAsync(CancellationToken cancellationToken) =>
        await sslStream.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) == 0 && !transport.TransportEnded;
}
