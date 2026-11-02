namespace Curl.Conformance.Extended.Kafka.Compression;

/// <summary>The Snappy copy element, by the width of its offset, that <see cref="KafkaReferenceSnappyEncoder"/> writes.</summary>
public enum KafkaSnappyCopy
{
    /// <summary>Tag 01: length 4 to 11 and an 11-bit offset, three bits in the tag and one byte after it.</summary>
    OneByteOffset,

    /// <summary>Tag 10: length 1 to 64 and a two-byte little-endian offset.</summary>
    TwoByteOffset,

    /// <summary>Tag 11: length 1 to 64 and a four-byte little-endian offset.</summary>
    FourByteOffset,
}
