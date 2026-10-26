using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// A transfer's plaintext TCP connection under <c>curl_multi_socket_action</c>: each read waits until the multi
/// handle signals the socket readable and each write until it signals it writable
/// (<see cref="TransferSocketWait" />), so the transfer never takes bytes the application's poll has not seen and
/// the application is woken again when more come (ADR-0821, BL-3847).
/// </summary>
/// <remarks>
/// It wraps the dialed connection beneath TLS and any HTTP/2 framing, so a read those layers answer from bytes they
/// already hold never reaches it and never waits on a signal. Every other member is the inner connection's.
/// </remarks>
/// <param name="inner">The dialed connection.</param>
/// <param name="wait">What the transfer waits for on the connection's socket.</param>
internal sealed class SocketActionGatedConnection(IConnection inner, TransferSocketWait wait) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => inner.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => inner.LocalEndPoint;

    /// <inheritdoc />
    public nint? SocketHandle => inner.SocketHandle;

    /// <inheritdoc />
    public IConnectionSession? Session => inner.Session;

    /// <inheritdoc />
    public bool IsSharedWithAnotherTransfer => inner.IsSharedWithAnotherTransfer;

    /// <inheritdoc />
    public bool HasPeerClosed => inner.HasPeerClosed;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        await wait.WaitUntilReadableAsync(cancellationToken).ConfigureAwait(false);
        return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await wait.WaitUntilWritableAsync(cancellationToken).ConfigureAwait(false);
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public void MarkReusable() => inner.MarkReusable();

    /// <inheritdoc />
    public bool TryHoldSession(IConnectionSession session) => inner.TryHoldSession(session);

    /// <inheritdoc />
    public ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken) =>
        inner.ClearTlsAsync(sendCloseNotifyFirst, cancellationToken);

    /// <inheritdoc />
    public ValueTask ShutDownSendingAsync(CancellationToken cancellationToken) => inner.ShutDownSendingAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
