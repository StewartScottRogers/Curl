using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// A connection whose writes reach the server in pieces, as upstream's debug build sends them under
/// <c>CURL_SMALLREQSEND</c> and <c>CURL_SMALLSENDS</c> (BL-2832, ADR-0721): the first write in pieces of at most
/// <paramref name="firstWriteLimit"/> bytes, and every write in pieces of at most <paramref name="everyWriteLimit"/>
/// bytes. The bytes themselves are unchanged; everything else is the inner connection's.
/// </summary>
/// <param name="inner">The connection the pieces are written to.</param>
/// <param name="firstWriteLimit">The largest piece of the first write, or <see langword="null"/> for no limit of its own.</param>
/// <param name="everyWriteLimit">The largest piece of any write, or <see langword="null"/> for none.</param>
internal sealed class SmallSendsConnection(IConnection inner, int? firstWriteLimit, int? everyWriteLimit) : IConnection
{
    private bool written;

    public bool IsSecure => inner.IsSecure;

    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    public EndPoint? LocalEndPoint => inner.LocalEndPoint;

    public IConnectionSession? Session => inner.Session;

    public bool IsSharedWithAnotherTransfer => inner.IsSharedWithAnotherTransfer;

    public bool HasPeerClosed => inner.HasPeerClosed;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken) => inner.ReadAsync(buffer, cancellationToken);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        int pieceLimit = PieceLimit();
        written = true;
        for (int offset = 0; offset < buffer.Length; offset += pieceLimit)
        {
            await inner.WriteAsync(buffer.Slice(offset, Math.Min(pieceLimit, buffer.Length - offset)), cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public void MarkReusable() => inner.MarkReusable();

    public bool TryHoldSession(IConnectionSession session) => inner.TryHoldSession(session);

    public async ValueTask<IConnection?> ClearTlsAsync(bool sendCloseNotifyFirst, CancellationToken cancellationToken) =>
        await inner.ClearTlsAsync(sendCloseNotifyFirst, cancellationToken).ConfigureAwait(false) is { } cleared
            ? new SmallSendsConnection(cleared, firstWriteLimit, everyWriteLimit)
            : null;

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    // The smaller of the limits that apply to this write; int.MaxValue when none does.
    private int PieceLimit() =>
        Math.Min(written ? int.MaxValue : firstWriteLimit ?? int.MaxValue, everyWriteLimit ?? int.MaxValue);
}
