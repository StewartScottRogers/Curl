namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

/// <summary>
/// Writes a zstd backward bit stream (RFC 8878 section 4.1): fields are appended from the lowest bit of the first byte
/// up, and <see cref="ToArray"/> ends the stream with a one marker bit, so a decoder reading from the last byte down
/// reads the field written last first.
/// </summary>
public sealed class KafkaReferenceFseBackwardBitWriter
{
    private readonly List<bool> bits = [];

    /// <summary>The bits written so far, the marker not counted.</summary>
    public int BitCount => bits.Count;

    /// <summary>Appends the low <paramref name="width"/> bits of <paramref name="value"/>.</summary>
    /// <param name="value">The field's value.</param>
    /// <param name="width">Its width in bits, 0 to 31.</param>
    public void WriteBits(int value, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, 31);
        for (int bit = 0; bit < width; bit++)
        {
            bits.Add(((value >> bit) & 1) != 0);
        }
    }

    /// <summary>The stream: the bits written, the marker bit, and zero bits up to the byte boundary.</summary>
    /// <returns>Its bytes.</returns>
    public byte[] ToArray()
    {
        byte[] stream = new byte[(bits.Count / 8) + 1];
        for (int index = 0; index < bits.Count; index++)
        {
            stream[index / 8] |= (byte)(bits[index] ? 1 << (index % 8) : 0);
        }

        stream[bits.Count / 8] |= (byte)(1 << (bits.Count % 8));
        return stream;
    }
}
