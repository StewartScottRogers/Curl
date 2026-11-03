using System.Buffers.Binary;

namespace Curl.Conformance.Extended.Kafka.Records;

/// <summary>
/// One partition's log: record batches stored as their bytes, each given the next offset as its base offset when
/// appended, and the high watermark one past the last offset stored.
/// </summary>
public sealed class KafkaReferenceLog
{
    // lastOffsetDelta sits after baseOffset, batchLength, partitionLeaderEpoch, magic, crc and attributes.
    private const int LastOffsetDeltaPosition = sizeof(long) + sizeof(int) + sizeof(int) + sizeof(sbyte) + sizeof(uint) + sizeof(short);

    private readonly List<(long BaseOffset, long LastOffset, byte[] Bytes)> batches = [];

    /// <summary>Gets the offset after the last record stored, 0 for an empty partition.</summary>
    public long HighWatermark { get; private set; }

    /// <summary>Stores a batch at the high watermark, writing that as its base offset (which its CRC does not cover).</summary>
    /// <param name="batch">The batch's bytes; their CRC is stored as it is, good or bad.</param>
    /// <returns>The base offset given to it.</returns>
    public long Append(byte[] batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        long baseOffset = HighWatermark;
        byte[] stored = [.. batch];
        BinaryPrimitives.WriteInt64BigEndian(stored, baseOffset);
        long lastOffset = baseOffset + BinaryPrimitives.ReadInt32BigEndian(stored.AsSpan(LastOffsetDeltaPosition));
        batches.Add((baseOffset, lastOffset, stored));
        HighWatermark = lastOffset + 1;
        return baseOffset;
    }

    /// <summary>
    /// The bytes of every batch holding <paramref name="offset"/> or a later one, the first of which may start before
    /// it, cut at <paramref name="maxBytes"/> so the last batch may be partial, as a broker answers a fetch.
    /// </summary>
    /// <param name="offset">The fetch offset.</param>
    /// <param name="maxBytes">The most bytes to answer.</param>
    /// <returns>The bytes.</returns>
    public byte[] ReadFrom(long offset, int maxBytes)
    {
        byte[] all = [.. batches.Where(batch => batch.LastOffset >= offset).SelectMany(batch => batch.Bytes)];
        return all.Length > maxBytes ? all[..maxBytes] : all;
    }
}
