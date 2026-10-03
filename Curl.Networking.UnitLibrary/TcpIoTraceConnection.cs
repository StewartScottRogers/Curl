using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every call on to <paramref name="inner" /> and writes the <c>[TCP]</c> lines curl 8.21.0
/// writes for a plain HTTP connection's I/O under <c>--trace-config tcp</c>, <c>network</c> or
/// <c>all</c> (measured, BL-1195 Notes): <c>[TCP] send(len=&lt;n&gt;) -&gt; 0, &lt;n&gt;</c> after
/// each write, which comes before the request's <c>&gt;</c> lines, as the HTTP handler reports its
/// head once it is written, and <c>[TCP] recv(len=102400) -&gt; 0, &lt;n&gt;</c> after each read.
/// </summary>
/// <remarks>
/// A read that cannot complete at once is first written as curl's would-block result,
/// <c>[TCP] recv(len=102400) -&gt; 81, 0</c> (<c>CURLE_AGAIN</c>), as curl writes one when it
/// polls before the server has answered. The length is always curl's receive buffer, 102400,
/// whatever the reader's own buffer (ADR-0357's BL-1195 amendment).
/// </remarks>
/// <param name="inner">The dialled connection.</param>
/// <param name="events">The events the lines are written to.</param>
internal sealed class TcpIoTraceConnection(IConnection inner, ITransferEvents events) : IConnection
{
    /// <summary>The length curl's lines give every read: its receive buffer's size.</summary>
    public const int ReceiveBufferLength = 102400;

    /// <summary>The line written for a read that cannot complete at once.</summary>
    public const string WouldBlockLine = "[TCP] recv(len=102400) -> 81, 0";

    /// <inheritdoc />
    public bool IsSecure => inner.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    /// <inheritdoc />
    public EndPoint? LocalEndPoint => inner.LocalEndPoint;

    /// <inheritdoc />
    public IConnectionSession? Session => inner.Session;

    /// <inheritdoc />
    public bool IsSharedWithAnotherTransfer => inner.IsSharedWithAnotherTransfer;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var reading = inner.ReadAsync(buffer, cancellationToken);
        if (!reading.IsCompleted)
        {
            events.ReportInfo(WouldBlockLine);
        }

        var read = await reading.ConfigureAwait(false);
        events.ReportInfo($"[TCP] recv(len={ReceiveBufferLength}) -> 0, {read}");
        return read;
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        events.ReportInfo($"[TCP] send(len={buffer.Length}) -> 0, {buffer.Length}");
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
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
