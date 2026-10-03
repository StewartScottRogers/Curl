using System.Net;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Passes every call on to <paramref name="inner" /> and writes the TCP filter lines curl 8.21.0
/// writes for a plain connection's I/O under <c>--trace-config tcp</c>, <c>network</c> or
/// <c>all</c>, shaped by <paramref name="lines" />: <c>[TCP] send(len=&lt;n&gt;) -&gt; 0, &lt;n&gt;</c>
/// after each write, which comes before the request's <c>&gt;</c> lines, as a handler reports what
/// it sent once it is written, and <c>[TCP] recv(len=&lt;length&gt;) -&gt; 0, &lt;n&gt;</c> after each
/// read (measured, BL-1195 and BL-1259 Notes).
/// </summary>
/// <remarks>
/// Under <see cref="TcpIoTraceLines.WritesWouldBlockReads" />, a read that cannot complete at once is
/// first written as curl's would-block result, <c>recv(len=&lt;length&gt;) -&gt; 81, 0</c>
/// (<c>CURLE_AGAIN</c>), as curl writes one when it polls before the server has answered. The length
/// is <see cref="TcpIoTraceLines.ReceiveLength" />, such as HTTP's 102400 whatever the reader's own
/// buffer, or the read's buffer length when that is <see langword="null" /> (ADR-0357's BL-1195 and
/// BL-1259 amendments).
/// </remarks>
/// <param name="inner">The dialled connection.</param>
/// <param name="events">The events the lines are written to.</param>
/// <param name="lines">The filter name, receive length and would-block choice the lines carry.</param>
internal sealed class TcpIoTraceConnection(IConnection inner, ITransferEvents events, TcpIoTraceLines lines) : IConnection
{
    /// <summary>How a plain HTTP connection's I/O is written: <c>[TCP]</c>, curl's 102400-byte receive buffer as every read's length, would-block reads written.</summary>
    public static readonly TcpIoTraceLines HttpLines = new("TCP", 102400, WritesWouldBlockReads: true);

    /// <summary>How an https connection's TLS handshake records are written: <c>[TCP]</c>, curl's Schannel build's 4096-byte handshake buffer as every read's length, would-block reads written (ADR-0357's BL-1260 amendment).</summary>
    public static readonly TcpIoTraceLines HttpsHandshakeLines = new("TCP", 4096, WritesWouldBlockReads: true);

    /// <summary>How an https connection's TLS application-data records are written: <c>[TCP]</c>, curl's Schannel build's 103424-byte receive buffer as every read's length, would-block reads written (ADR-0357's BL-1260 amendment).</summary>
    public static readonly TcpIoTraceLines HttpsApplicationDataLines = new("TCP", 103424, WritesWouldBlockReads: true);

    /// <summary>
    /// The filter name, receive length and would-block choice the next lines carry: at first
    /// the constructor's lines, changed by an https connection from its handshake's lines to its
    /// application data's once the handshake completes.
    /// </summary>
    public TcpIoTraceLines Lines { get; set; } = lines;

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
        var length = Lines.ReceiveLength ?? buffer.Length;
        var reading = inner.ReadAsync(buffer, cancellationToken);
        if (Lines.WritesWouldBlockReads && !reading.IsCompleted)
        {
            events.ReportInfo($"[{Lines.FilterName}] recv(len={length}) -> 81, 0");
        }

        var read = await reading.ConfigureAwait(false);
        events.ReportInfo($"[{Lines.FilterName}] recv(len={length}) -> 0, {read}");
        return read;
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        events.ReportInfo($"[{Lines.FilterName}] send(len={buffer.Length}) -> 0, {buffer.Length}");
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
