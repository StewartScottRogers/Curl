using Curl.Conformance.Extended.Kafka.Compression.Entropy.Fse;

namespace Curl.Conformance.Extended.Kafka.Compression.Entropy.Sequences;

/// <summary>
/// How a sequences section codes one of its three codes (RFC 8878 section 3.1.1.3.2.1): its compression mode in the
/// Symbol_Compression_Modes byte, the FSE table the codes are encoded with, and the table description that follows
/// the header (only FSE_Compressed_Mode has one). Predefined_Mode uses the default distribution; Repeat_Mode reuses
/// the table of the frame's previous compressed block, which the caller names.
/// </summary>
public sealed class KafkaReferenceSequenceCodeTable
{
    /// <summary>Predefined_Mode's value in the Symbol_Compression_Modes byte.</summary>
    public const int PredefinedMode = 0;

    /// <summary>FSE_Compressed_Mode's value in the Symbol_Compression_Modes byte.</summary>
    public const int FseCompressedMode = 2;

    /// <summary>Repeat_Mode's value in the Symbol_Compression_Modes byte.</summary>
    public const int RepeatMode = 3;

    private readonly byte[] description;

    private KafkaReferenceSequenceCodeTable(int mode, KafkaReferenceFseEncodingTable table, byte[] description)
    {
        Mode = mode;
        Table = table;
        this.description = description;
    }

    /// <summary>Literals length codes in Predefined_Mode.</summary>
    public static KafkaReferenceSequenceCodeTable PredefinedLiteralsLength { get; } = new(PredefinedMode, KafkaReferenceFseEncodingTable.PredefinedLiteralsLength, []);

    /// <summary>Offset codes in Predefined_Mode.</summary>
    public static KafkaReferenceSequenceCodeTable PredefinedOffset { get; } = new(PredefinedMode, KafkaReferenceFseEncodingTable.PredefinedOffset, []);

    /// <summary>Match length codes in Predefined_Mode.</summary>
    public static KafkaReferenceSequenceCodeTable PredefinedMatchLength { get; } = new(PredefinedMode, KafkaReferenceFseEncodingTable.PredefinedMatchLength, []);

    /// <summary>The mode, as the Symbol_Compression_Modes byte holds it.</summary>
    public int Mode { get; }

    /// <summary>The FSE table the codes are encoded with.</summary>
    public KafkaReferenceFseEncodingTable Table { get; }

    /// <summary>The table description written after the header: empty but in FSE_Compressed_Mode.</summary>
    public IReadOnlyList<byte> Description => description;

    /// <summary>FSE_Compressed_Mode: <paramref name="table"/>, described after the header.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The code table.</returns>
    public static KafkaReferenceSequenceCodeTable FseCompressed(KafkaReferenceFseEncodingTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return new(FseCompressedMode, table, KafkaReferenceFseTableDescriptionWriter.Write(table.NormalizedCounts, table.AccuracyLog));
    }

    /// <summary>Repeat_Mode: the codes reuse <paramref name="previous"/>, the table of the frame's previous compressed block.</summary>
    /// <param name="previous">That block's code table.</param>
    /// <returns>The code table.</returns>
    public static KafkaReferenceSequenceCodeTable Repeat(KafkaReferenceSequenceCodeTable previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        return new(RepeatMode, previous.Table, []);
    }
}
