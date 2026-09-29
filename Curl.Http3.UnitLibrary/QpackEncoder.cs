using Curl.Http2;

namespace Curl.Http3;

/// <summary>
/// The encoding half of QPACK (RFC 9204): encodes field sections against the static table
/// and a dynamic table, writes the encoder stream instructions that build the peer
/// decoder's copy of that table, and reads the peer decoder's acknowledgements so that it
/// never evicts an entry the decoder may still need.
/// </summary>
/// <remarks>
/// <para>
/// The dynamic table stays empty until <see cref="TrySetDynamicTableCapacity" /> gives it
/// room, so by default every section is encoded with the static table and literals only.
/// </para>
/// <para>
/// A field line is encoded as the first of these that applies: a static entry matching
/// name and value; the newest dynamic entry matching both; a new dynamic entry inserted
/// for it; a literal naming a static entry; a literal naming the newest dynamic entry with
/// the name; a literal with a literal name. A never-indexed field skips every dynamic step
/// and every indexed form. A dynamic entry the decoder has not acknowledged is referenced,
/// and inserted, only while the stream may block (SETTINGS_QPACK_BLOCKED_STREAMS). Base is
/// the Insert Count before the section, so entries inserted for it are post-Base, or 0 when
/// the section references no dynamic entry.
/// </para>
/// </remarks>
public sealed class QpackEncoder
{
    private const QpackErrorCode DecoderStreamError = QpackErrorCode.DecoderStreamError;

    private readonly QpackDynamicTable table = new();
    private readonly long maximumTableCapacity;
    private readonly long maximumBlockedStreams;
    private readonly bool huffmanCodeLiterals;
    private readonly List<byte> encoderStreamBytes = [];
    private readonly List<byte> unreadDecoderStreamBytes = [];
    private readonly List<UnacknowledgedSection> unacknowledgedSections = [];
    private long knownReceivedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="QpackEncoder" /> class.
    /// </summary>
    /// <param name="maximumTableCapacity">The peer's SETTINGS_QPACK_MAX_TABLE_CAPACITY.</param>
    /// <param name="maximumBlockedStreams">The peer's SETTINGS_QPACK_BLOCKED_STREAMS.</param>
    /// <param name="huffmanCodeLiterals">
    /// Whether string literals are Huffman-coded when that makes them strictly shorter.
    /// </param>
    public QpackEncoder(long maximumTableCapacity, long maximumBlockedStreams, bool huffmanCodeLiterals = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTableCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBlockedStreams);
        this.maximumTableCapacity = maximumTableCapacity;
        this.maximumBlockedStreams = maximumBlockedStreams;
        this.huffmanCodeLiterals = huffmanCodeLiterals;
    }

    /// <summary>Gets how many entries this encoder has inserted into the dynamic table.</summary>
    public long InsertCount => table.InsertCount;

    /// <summary>Gets how many insertions the peer decoder has acknowledged, the Known Received Count.</summary>
    public long KnownReceivedCount => knownReceivedCount;

    /// <summary>Gets the dynamic table's capacity.</summary>
    public long DynamicTableCapacity => table.Capacity;

    /// <summary>Gets the dynamic table's size (RFC 9204 section 3.2.1).</summary>
    public long DynamicTableSize => table.Size;

    /// <summary>
    /// Sets the dynamic table's capacity and queues a Set Dynamic Table Capacity instruction
    /// (RFC 9204 section 4.3.1), unless shrinking would evict an entry the decoder may still
    /// need.
    /// </summary>
    /// <param name="capacity">The capacity, at most the peer's maximum.</param>
    /// <returns>Whether the capacity was set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The capacity is negative or above the peer's maximum.</exception>
    public bool TrySetDynamicTableCapacity(long capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, maximumTableCapacity);
        if (table.DroppedCountAfterFitting(0, capacity) > GetEvictionLimit(long.MaxValue))
        {
            return false;
        }

        QpackPrimitives.WriteInteger(encoderStreamBytes, capacity, 5, 0x20);
        table.SetCapacity(capacity);
        return true;
    }

    /// <summary>
    /// Inserts a field into the dynamic table without encoding a section, and queues the
    /// Insert instruction (RFC 9204 sections 4.3.2 and 4.3.3), naming a static entry or the
    /// newest dynamic entry with the name when there is one.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>Whether it was inserted; not when it cannot fit without evicting an entry the decoder may still need.</returns>
    public bool TryInsert(HeaderField field) => TryInsert(field, long.MaxValue);

    /// <summary>
    /// Inserts a copy of a dynamic table entry and queues a Duplicate instruction (RFC 9204
    /// section 4.3.4).
    /// </summary>
    /// <param name="absoluteIndex">The entry's absolute index.</param>
    /// <returns>Whether it was duplicated; not when the copy cannot fit without evicting an entry the decoder may still need.</returns>
    /// <exception cref="ArgumentOutOfRangeException">No entry has that absolute index.</exception>
    public bool TryDuplicate(long absoluteIndex)
    {
        if (!table.Contains(absoluteIndex))
        {
            throw new ArgumentOutOfRangeException(nameof(absoluteIndex), absoluteIndex, "No dynamic table entry has this absolute index.");
        }

        var entry = table.Get(absoluteIndex);
        if (!CanFit(entry.Size, long.MaxValue))
        {
            return false;
        }

        QpackPrimitives.WriteInteger(encoderStreamBytes, table.InsertCount - 1 - absoluteIndex, 5, 0x00);
        table.Insert(entry);
        return true;
    }

    /// <summary>
    /// Encodes a field section for a stream, queuing on the encoder stream any entries it
    /// inserts on the way.
    /// </summary>
    /// <param name="streamId">The request stream the section will be sent on.</param>
    /// <param name="fields">The field lines, in order.</param>
    /// <returns>The encoded field section, prefix included.</returns>
    public byte[] EncodeFieldSection(long streamId, IReadOnlyList<HeaderField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var section = new SectionInProgress(table.InsertCount, CanBlock(streamId));
        foreach (var field in fields)
        {
            EncodeFieldLine(field, section);
        }

        var prefix = new List<byte>();
        var maximumEntries = QpackRequiredInsertCount.GetMaximumEntries(maximumTableCapacity);
        QpackPrimitives.WriteInteger(prefix, QpackRequiredInsertCount.Encode(section.RequiredInsertCount, maximumEntries), 8, 0x00);
        WriteDeltaBase(prefix, section.RequiredInsertCount == 0 ? 0 : section.BaseIndex, section.RequiredInsertCount);
        if (section.RequiredInsertCount > 0)
        {
            unacknowledgedSections.Add(new UnacknowledgedSection(streamId, section.RequiredInsertCount, section.SmallestReference));
        }

        prefix.AddRange(section.Lines);
        return [.. prefix];
    }

    /// <summary>
    /// Applies bytes read from the peer's decoder stream: Section Acknowledgements, Stream
    /// Cancellations and Insert Count Increments (RFC 9204 section 4.4). An instruction the
    /// bytes end inside waits for the next call.
    /// </summary>
    /// <param name="bytes">The next bytes of the decoder stream.</param>
    /// <exception cref="QpackException">
    /// An instruction is invalid: an acknowledgement for a stream with no section waiting
    /// for one, or an increment of zero or past the Insert Count
    /// (<see cref="QpackErrorCode.DecoderStreamError" />).
    /// </exception>
    public void ReadDecoderStream(ReadOnlySpan<byte> bytes)
    {
        unreadDecoderStreamBytes.AddRange(bytes);
        var buffer = unreadDecoderStreamBytes.ToArray();
        var consumed = 0;
        try
        {
            while (consumed < buffer.Length)
            {
                var position = consumed;
                ExecuteDecoderInstruction(buffer, ref position);
                consumed = position;
            }
        }
        catch (QpackIncompleteInstructionException)
        {
            // The rest of the instruction has not arrived; it is kept for the next call.
        }

        unreadDecoderStreamBytes.RemoveRange(0, consumed);
    }

    /// <summary>
    /// Takes the encoder stream instructions queued so far.
    /// </summary>
    /// <returns>The bytes to write to this endpoint's encoder stream, possibly none.</returns>
    public byte[] TakeEncoderStreamBytes()
    {
        var bytes = encoderStreamBytes.ToArray();
        encoderStreamBytes.Clear();
        return bytes;
    }

    private static void WriteDeltaBase(List<byte> prefix, long baseIndex, long requiredInsertCount)
    {
        if (baseIndex >= requiredInsertCount)
        {
            QpackPrimitives.WriteInteger(prefix, baseIndex - requiredInsertCount, 7, 0x00);
            return;
        }

        QpackPrimitives.WriteInteger(prefix, requiredInsertCount - baseIndex - 1, 7, 0x80);
    }

    private void EncodeFieldLine(HeaderField field, SectionInProgress section)
    {
        var (staticIndex, isStaticExact) = QpackStaticTable.Find(field.Name, field.Value);
        if (field.IsNeverIndexed)
        {
            WriteLiteralWithoutDynamicReference(field, staticIndex, section);
            return;
        }

        if (isStaticExact)
        {
            QpackPrimitives.WriteInteger(section.Lines, staticIndex, 6, 0xC0);
            return;
        }

        if (!TryWriteDynamicIndexed(field, section))
        {
            WriteLiteral(field, staticIndex, section);
        }
    }

    private bool TryWriteDynamicIndexed(HeaderField field, SectionInProgress section)
    {
        var absoluteIndex = table.FindNewest(field.Name, field.Value, true, section.LargestUsableIndex(knownReceivedCount));
        if (absoluteIndex < 0 && section.CanBlock && TryInsert(field, section.SmallestReference))
        {
            absoluteIndex = table.InsertCount - 1;
        }

        if (absoluteIndex < 0)
        {
            return false;
        }

        section.Reference(absoluteIndex);
        if (absoluteIndex < section.BaseIndex)
        {
            QpackPrimitives.WriteInteger(section.Lines, section.BaseIndex - 1 - absoluteIndex, 6, 0x80);
            return true;
        }

        QpackPrimitives.WriteInteger(section.Lines, absoluteIndex - section.BaseIndex, 4, 0x10);
        return true;
    }

    private void WriteLiteral(HeaderField field, int staticIndex, SectionInProgress section)
    {
        var nameIndex = staticIndex < 0
            ? table.FindNewest(field.Name, string.Empty, false, section.LargestUsableIndex(knownReceivedCount))
            : -1;
        if (nameIndex < 0)
        {
            WriteLiteralWithoutDynamicReference(field, staticIndex, section);
            return;
        }

        section.Reference(nameIndex);
        if (nameIndex < section.BaseIndex)
        {
            QpackPrimitives.WriteInteger(section.Lines, section.BaseIndex - 1 - nameIndex, 4, 0x40);
        }
        else
        {
            QpackPrimitives.WriteInteger(section.Lines, nameIndex - section.BaseIndex, 3, 0x00);
        }

        QpackPrimitives.WriteString(section.Lines, field.Value, 7, 0x00, huffmanCodeLiterals);
    }

    private void WriteLiteralWithoutDynamicReference(HeaderField field, int staticIndex, SectionInProgress section)
    {
        if (staticIndex >= 0)
        {
            QpackPrimitives.WriteInteger(section.Lines, staticIndex, 4, (byte)(field.IsNeverIndexed ? 0x70 : 0x50));
        }
        else
        {
            QpackPrimitives.WriteString(section.Lines, field.Name, 3, (byte)(field.IsNeverIndexed ? 0x30 : 0x20), huffmanCodeLiterals);
        }

        QpackPrimitives.WriteString(section.Lines, field.Value, 7, 0x00, huffmanCodeLiterals);
    }

    private bool TryInsert(HeaderField field, long smallestReferenceInProgress)
    {
        if (!CanFit(field.Size, smallestReferenceInProgress))
        {
            return false;
        }

        WriteInsertInstruction(field);
        table.Insert(field);
        return true;
    }

    private void WriteInsertInstruction(HeaderField field)
    {
        var (staticIndex, _) = QpackStaticTable.Find(field.Name, field.Value);
        var nameIndex = table.FindNewest(field.Name, string.Empty, false, long.MaxValue);
        if (staticIndex >= 0)
        {
            QpackPrimitives.WriteInteger(encoderStreamBytes, staticIndex, 6, 0xC0);
        }
        else if (nameIndex >= 0)
        {
            QpackPrimitives.WriteInteger(encoderStreamBytes, table.InsertCount - 1 - nameIndex, 6, 0x80);
        }
        else
        {
            QpackPrimitives.WriteString(encoderStreamBytes, field.Name, 5, 0x40, huffmanCodeLiterals);
        }

        QpackPrimitives.WriteString(encoderStreamBytes, field.Value, 7, 0x00, huffmanCodeLiterals);
    }

    /// <summary>
    /// Gets whether an entry of <paramref name="size" /> fits once the oldest entries are
    /// evicted, evicting none the decoder has not acknowledged or that a section still
    /// waiting for acknowledgement, or the section in progress, references.
    /// </summary>
    private bool CanFit(long size, long smallestReferenceInProgress) =>
        size <= table.Capacity
        && table.DroppedCountAfterFitting(size, table.Capacity) <= GetEvictionLimit(smallestReferenceInProgress);

    private long GetEvictionLimit(long smallestReferenceInProgress)
    {
        var limit = Math.Min(knownReceivedCount, smallestReferenceInProgress);
        foreach (var section in unacknowledgedSections)
        {
            limit = Math.Min(limit, section.SmallestReference);
        }

        return limit;
    }

    private bool CanBlock(long streamId)
    {
        var blockingStreams = new HashSet<long>();
        foreach (var section in unacknowledgedSections)
        {
            if (section.RequiredInsertCount > knownReceivedCount)
            {
                blockingStreams.Add(section.StreamId);
            }
        }

        return blockingStreams.Contains(streamId) || blockingStreams.Count < maximumBlockedStreams;
    }

    private void ExecuteDecoderInstruction(ReadOnlySpan<byte> buffer, ref int position)
    {
        var first = buffer[position];
        switch (first)
        {
            case >= 0x80:
                AcknowledgeSection(QpackPrimitives.ReadInteger(buffer, ref position, 7, DecoderStreamError));
                break;
            case >= 0x40:
                var streamId = QpackPrimitives.ReadInteger(buffer, ref position, 6, DecoderStreamError);
                unacknowledgedSections.RemoveAll(section => section.StreamId == streamId);
                break;
            default:
                IncrementKnownReceivedCount(QpackPrimitives.ReadInteger(buffer, ref position, 6, DecoderStreamError));
                break;
        }
    }

    private void AcknowledgeSection(long streamId)
    {
        var index = unacknowledgedSections.FindIndex(section => section.StreamId == streamId);
        QpackPrimitives.ThrowIf(index < 0, DecoderStreamError, $"stream {streamId} has no field section waiting for acknowledgement");
        knownReceivedCount = Math.Max(knownReceivedCount, unacknowledgedSections[index].RequiredInsertCount);
        unacknowledgedSections.RemoveAt(index);
    }

    private void IncrementKnownReceivedCount(long increment)
    {
        QpackPrimitives.ThrowIf(
            increment == 0 || increment > table.InsertCount - knownReceivedCount,
            DecoderStreamError,
            $"an Insert Count Increment of {increment} is zero or past the Insert Count");
        knownReceivedCount += increment;
    }

    /// <summary>A field section sent on a stream whose Section Acknowledgement has not arrived.</summary>
    /// <param name="StreamId">The stream.</param>
    /// <param name="RequiredInsertCount">The section's Required Insert Count.</param>
    /// <param name="SmallestReference">The smallest absolute index the section references.</param>
    private sealed record UnacknowledgedSection(long StreamId, long RequiredInsertCount, long SmallestReference);

    /// <summary>The state of a field section while its lines are encoded.</summary>
    /// <param name="baseIndex">The section's Base: the Insert Count before it.</param>
    /// <param name="canBlock">Whether the section may reference entries the decoder has not acknowledged.</param>
    private sealed class SectionInProgress(long baseIndex, bool canBlock)
    {
        public long BaseIndex { get; } = baseIndex;

        public bool CanBlock { get; } = canBlock;

        public List<byte> Lines { get; } = [];

        public long RequiredInsertCount { get; private set; }

        public long SmallestReference { get; private set; } = long.MaxValue;

        public long LargestUsableIndex(long knownReceivedCount) => CanBlock ? long.MaxValue : knownReceivedCount - 1;

        public void Reference(long absoluteIndex)
        {
            RequiredInsertCount = Math.Max(RequiredInsertCount, absoluteIndex + 1);
            SmallestReference = Math.Min(SmallestReference, absoluteIndex);
        }
    }
}
