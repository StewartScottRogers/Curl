using Curl.Quic;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// A stream the client opened on <see cref="Http3ServerConnection" />, as the server reads it:
/// its STREAM frames put back in order (a repeat is dropped) and its final size once a FIN
/// has arrived (RFC 9000 sections 2 and 3).
/// </summary>
public sealed class Http3ServerStream
{
    private readonly QuicCryptoReassembler reassembler = new();

    private readonly List<byte> readable = [];

    private ulong receivedLength;

    private ulong? finalSize;

    /// <summary>Initializes a new instance of the <see cref="Http3ServerStream" /> class.</summary>
    /// <param name="id">The stream ID.</param>
    internal Http3ServerStream(ulong id) => Id = id;

    /// <summary>Gets the stream ID.</summary>
    public ulong Id { get; }

    /// <summary>Gets a value indicating whether the stream is unidirectional.</summary>
    public bool IsUnidirectional => (Id & 2) != 0;

    /// <summary>Gets a value indicating whether the FIN has arrived with every byte before it.</summary>
    public bool IsReadComplete => receivedLength == finalSize;

    /// <summary>Returns the bytes that have arrived in order since the last read.</summary>
    /// <returns>The bytes; empty when none wait.</returns>
    public byte[] Read()
    {
        byte[] bytes = [.. readable];
        readable.Clear();
        return bytes;
    }

    /// <summary>Takes one STREAM frame of the stream.</summary>
    /// <param name="frame">The frame.</param>
    internal void Receive(QuicStreamFrame frame)
    {
        var bytes = reassembler.Receive(new QuicCryptoFrame(frame.Offset, frame.Data));
        readable.AddRange(bytes);
        receivedLength += (ulong)bytes.Length;
        if (frame.IsFin)
        {
            finalSize = frame.Offset + (ulong)frame.Data.Length;
        }
    }
}
