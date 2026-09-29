using Curl.Tls;

namespace Curl.Quic;

/// <summary>
/// Randomness that is the same on every run: each byte asked for is the next value of a
/// counter from <see cref="FirstByte" />, and the X25519 key share has a fixed private key,
/// so the client's first Initial datagram can be pinned.
/// </summary>
internal sealed class QuicTestRandomSource : ITlsRandomSource
{
    private byte next;

    /// <summary>Gets the first byte handed out.</summary>
    public byte FirstByte { get; init; }

    /// <summary>Gets how many bytes have been handed out.</summary>
    public int BytesHandedOut { get; private set; }

    public void Fill(Span<byte> destination)
    {
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = (byte)(FirstByte + next++);
        }

        BytesHandedOut += destination.Length;
    }

    public Tls13KeyShare CreateKeyShare(ushort group)
    {
        Assert.AreEqual(TlsNamedGroup.X25519, group);
        return new X25519KeyShare([.. Enumerable.Range(0x40, 32).Select(value => (byte)value)]);
    }
}
