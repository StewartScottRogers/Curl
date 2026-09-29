using System.Buffers;

namespace Curl.Protocol.Pop3;

/// <summary>
/// The body bytes <see cref="Pop3BodyDecoder" /> lets go of for one received chunk, kept as the
/// separate pieces curl 8.21.0's <c>pop3_write</c> hands on, since <c>--trace</c> shows each
/// piece as its own <c>Recv data</c> block: a line's text, then its CRLF (BL-552).
/// </summary>
internal sealed class Pop3BodyPieces
{
    private readonly ArrayBufferWriter<byte> bytes = new();

    /// <summary>Where each piece ends in <see cref="bytes" />.</summary>
    private readonly List<int> ends = [];

    /// <summary>Gets how many pieces are held.</summary>
    public int Count => ends.Count;

    /// <summary>Gets how many bytes the pieces hold together.</summary>
    public int Length => bytes.WrittenCount;

    /// <summary>
    /// Gets the piece at <paramref name="index" />, valid until <see cref="Clear" />.
    /// </summary>
    /// <param name="index">The piece's position, from 0.</param>
    public ReadOnlyMemory<byte> this[int index] =>
        bytes.WrittenMemory[(index == 0 ? 0 : ends[index - 1])..ends[index]];

    /// <summary>
    /// Adds <paramref name="piece" />; an empty one is dropped, as curl writes no empty piece.
    /// </summary>
    /// <param name="piece">The bytes to add.</param>
    public void Add(ReadOnlySpan<byte> piece)
    {
        if (piece.IsEmpty)
        {
            return;
        }

        bytes.Write(piece);
        ends.Add(bytes.WrittenCount);
    }

    /// <summary>Drops every piece, ready for the next chunk.</summary>
    public void Clear()
    {
        bytes.ResetWrittenCount();
        ends.Clear();
    }
}
