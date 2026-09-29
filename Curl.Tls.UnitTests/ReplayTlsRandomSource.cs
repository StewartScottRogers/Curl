namespace Curl.Tls;

/// <summary>
/// An <see cref="ITlsRandomSource" /> that hands out fixed bytes and key shares in order,
/// so a handshake reproduces a trace: an empty request takes nothing from the queue.
/// </summary>
internal sealed class ReplayTlsRandomSource(IEnumerable<byte[]> fills, IEnumerable<Tls13KeyShare> keyShares) : ITlsRandomSource
{
    private readonly Queue<byte[]> fills = new(fills);
    private readonly Queue<Tls13KeyShare> keyShares = new(keyShares);

    public void Fill(Span<byte> destination)
    {
        if (destination.IsEmpty)
        {
            return;
        }

        byte[] next = fills.Dequeue();
        Assert.HasCount(destination.Length, next);
        next.CopyTo(destination);
    }

    public Tls13KeyShare CreateKeyShare(ushort group)
    {
        Tls13KeyShare next = keyShares.Dequeue();
        Assert.AreEqual(group, next.Group);
        return next;
    }
}
