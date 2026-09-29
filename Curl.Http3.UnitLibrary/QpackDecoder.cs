using System.Diagnostics.CodeAnalysis;
using Curl.Http2;

namespace Curl.Http3;

/// <summary>
/// The decoding half of QPACK (RFC 9204): applies the peer encoder's stream instructions to
/// a dynamic table, decodes encoded field sections against it, and produces the decoder
/// stream instructions that tell the peer encoder what has been received.
/// </summary>
/// <remarks>
/// The limits are the ones this endpoint advertised in its HTTP/3 <c>SETTINGS</c>. curl's
/// ngtcp2 build advertises 0 for both (ADR-0144), so its peers encode with the static table
/// and literals only; the decoder still implements the dynamic table for any other limits.
/// </remarks>
public sealed class QpackDecoder
{
    private const QpackErrorCode EncoderStreamError = QpackErrorCode.EncoderStreamError;

    private readonly QpackDynamicTable table = new();
    private readonly long maximumTableCapacity;
    private readonly long maximumBlockedStreams;
    private readonly HashSet<long> blockedStreams = [];
    private readonly List<byte> unreadEncoderStreamBytes = [];
    private readonly List<byte> decoderStreamBytes = [];
    private long knownReceivedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="QpackDecoder" /> class.
    /// </summary>
    /// <param name="maximumTableCapacity">The SETTINGS_QPACK_MAX_TABLE_CAPACITY this endpoint advertised.</param>
    /// <param name="maximumBlockedStreams">The SETTINGS_QPACK_BLOCKED_STREAMS this endpoint advertised.</param>
    public QpackDecoder(long maximumTableCapacity, long maximumBlockedStreams)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTableCapacity);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBlockedStreams);
        this.maximumTableCapacity = maximumTableCapacity;
        this.maximumBlockedStreams = maximumBlockedStreams;
    }

    /// <summary>Gets how many entries the peer encoder has inserted into the dynamic table.</summary>
    public long InsertCount => table.InsertCount;

    /// <summary>Gets the dynamic table's capacity, as the peer encoder last set it.</summary>
    public long DynamicTableCapacity => table.Capacity;

    /// <summary>Gets the dynamic table's size (RFC 9204 section 3.2.1).</summary>
    public long DynamicTableSize => table.Size;

    /// <summary>Gets how many streams are blocked waiting for dynamic table entries.</summary>
    public int BlockedStreamCount => blockedStreams.Count;

    /// <summary>
    /// Applies bytes read from the peer's encoder stream. Complete instructions take effect
    /// at once; an instruction the bytes end inside waits for the next call.
    /// </summary>
    /// <param name="bytes">The next bytes of the encoder stream.</param>
    /// <exception cref="QpackException">
    /// An instruction is invalid: a capacity above the advertised maximum, an entry larger
    /// than the capacity, or a reference to an entry that does not exist
    /// (<see cref="QpackErrorCode.EncoderStreamError" />).
    /// </exception>
    public void ReadEncoderStream(ReadOnlySpan<byte> bytes)
    {
        unreadEncoderStreamBytes.AddRange(bytes);
        var buffer = unreadEncoderStreamBytes.ToArray();
        var consumed = 0;
        try
        {
            while (consumed < buffer.Length)
            {
                var position = consumed;
                ExecuteEncoderInstruction(buffer, ref position);
                consumed = position;
            }
        }
        catch (QpackIncompleteInstructionException)
        {
            // The rest of the instruction has not arrived; it is kept for the next call.
        }

        unreadEncoderStreamBytes.RemoveRange(0, consumed);
    }

    /// <summary>
    /// Decodes an encoded field section, or reports that the stream is blocked because the
    /// section references entries the encoder stream has not delivered yet. A blocked
    /// section is decoded by calling again, with the same bytes, once
    /// <see cref="InsertCount" /> has grown.
    /// </summary>
    /// <param name="streamId">The request stream the section arrived on.</param>
    /// <param name="section">The whole encoded field section, prefix included.</param>
    /// <param name="fields">The field lines, when the section was decoded.</param>
    /// <returns><see langword="true" /> when decoded; <see langword="false" /> when the stream is blocked.</returns>
    /// <exception cref="QpackException">
    /// The section is invalid, or blocking it would exceed the advertised blocked-streams
    /// limit (<see cref="QpackErrorCode.DecompressionFailed" />).
    /// </exception>
    public bool TryDecodeFieldSection(long streamId, ReadOnlySpan<byte> section, [NotNullWhen(true)] out IReadOnlyList<HeaderField>? fields)
    {
        try
        {
            return TryDecode(streamId, section, out fields);
        }
        catch (QpackIncompleteInstructionException)
        {
            throw new QpackException(QpackErrorCode.DecompressionFailed, "the field section ends inside a field line");
        }
    }

    /// <summary>
    /// Records that a request stream was reset or abandoned before its field section was
    /// decoded, and queues a Stream Cancellation instruction (RFC 9204 section 4.4.2) when
    /// the dynamic table is in use.
    /// </summary>
    /// <param name="streamId">The stream.</param>
    public void CancelStream(long streamId)
    {
        blockedStreams.Remove(streamId);
        if (maximumTableCapacity > 0)
        {
            QpackPrimitives.WriteInteger(decoderStreamBytes, streamId, 6, 0x40);
        }
    }

    /// <summary>
    /// Takes the decoder stream instructions queued so far, ending with an Insert Count
    /// Increment (RFC 9204 section 4.4.3) for any insertions no acknowledgement covers.
    /// </summary>
    /// <returns>The bytes to write to this endpoint's decoder stream, possibly none.</returns>
    public byte[] TakeDecoderStreamBytes()
    {
        if (table.InsertCount > knownReceivedCount)
        {
            QpackPrimitives.WriteInteger(decoderStreamBytes, table.InsertCount - knownReceivedCount, 6, 0x00);
            knownReceivedCount = table.InsertCount;
        }

        var bytes = decoderStreamBytes.ToArray();
        decoderStreamBytes.Clear();
        return bytes;
    }

    private bool TryDecode(long streamId, ReadOnlySpan<byte> section, out IReadOnlyList<HeaderField>? fields)
    {
        var position = 0;
        var encodedInsertCount = QpackPrimitives.ReadInteger(section, ref position, 8, QpackErrorCode.DecompressionFailed);
        var requiredInsertCount = QpackRequiredInsertCount.Decode(
            encodedInsertCount,
            QpackRequiredInsertCount.GetMaximumEntries(maximumTableCapacity),
            table.InsertCount);
        var baseIndex = ReadBase(section, ref position, requiredInsertCount);
        if (requiredInsertCount > table.InsertCount)
        {
            Block(streamId);
            fields = null;
            return false;
        }

        blockedStreams.Remove(streamId);
        fields = new QpackFieldLineReader(section, position, table, requiredInsertCount, baseIndex).ReadAll();
        Acknowledge(streamId, requiredInsertCount);
        return true;
    }

    private static long ReadBase(ReadOnlySpan<byte> section, ref int position, long requiredInsertCount)
    {
        var isNegative = (QpackPrimitives.PeekByte(section, position) & 0x80) != 0;
        var deltaBase = QpackPrimitives.ReadInteger(section, ref position, 7, QpackErrorCode.DecompressionFailed);
        var baseIndex = isNegative ? requiredInsertCount - deltaBase - 1 : requiredInsertCount + deltaBase;
        QpackPrimitives.ThrowIf(
            baseIndex < 0 || baseIndex > QpackPrimitives.LargestInteger,
            QpackErrorCode.DecompressionFailed,
            "the Base is outside 0 to 2^62 - 1");
        return baseIndex;
    }

    private void Block(long streamId)
    {
        QpackPrimitives.ThrowIf(
            !blockedStreams.Contains(streamId) && blockedStreams.Count >= maximumBlockedStreams,
            QpackErrorCode.DecompressionFailed,
            "the Required Insert Count is beyond the dynamic table and no more streams may block");
        blockedStreams.Add(streamId);
    }

    private void Acknowledge(long streamId, long requiredInsertCount)
    {
        if (requiredInsertCount == 0)
        {
            return;
        }

        QpackPrimitives.WriteInteger(decoderStreamBytes, streamId, 7, 0x80);
        knownReceivedCount = Math.Max(knownReceivedCount, requiredInsertCount);
    }

    private void ExecuteEncoderInstruction(ReadOnlySpan<byte> buffer, ref int position)
    {
        var first = buffer[position];
        switch (first)
        {
            case >= 0x80:
                InsertWithNameReference(buffer, ref position, (first & 0x40) != 0);
                break;
            case >= 0x40:
                InsertWithLiteralName(buffer, ref position);
                break;
            case >= 0x20:
                SetDynamicTableCapacity(QpackPrimitives.ReadInteger(buffer, ref position, 5, EncoderStreamError));
                break;
            default:
                Insert(GetRelative(QpackPrimitives.ReadInteger(buffer, ref position, 5, EncoderStreamError)));
                break;
        }
    }

    private void InsertWithNameReference(ReadOnlySpan<byte> buffer, ref int position, bool isStatic)
    {
        var index = QpackPrimitives.ReadInteger(buffer, ref position, 6, EncoderStreamError);
        var name = isStatic ? QpackStaticTable.Get(index, EncoderStreamError).Name : GetRelative(index).Name;
        var value = QpackPrimitives.ReadString(buffer, ref position, 7, EncoderStreamError);
        Insert(new HeaderField(name, value));
    }

    private void InsertWithLiteralName(ReadOnlySpan<byte> buffer, ref int position)
    {
        var name = QpackPrimitives.ReadString(buffer, ref position, 5, EncoderStreamError);
        var value = QpackPrimitives.ReadString(buffer, ref position, 7, EncoderStreamError);
        Insert(new HeaderField(name, value));
    }

    private void SetDynamicTableCapacity(long capacity)
    {
        QpackPrimitives.ThrowIf(
            capacity > maximumTableCapacity,
            EncoderStreamError,
            $"the dynamic table capacity {capacity} exceeds the advertised maximum {maximumTableCapacity}");
        table.SetCapacity(capacity);
    }

    private HeaderField GetRelative(long relativeIndex)
    {
        var absoluteIndex = table.InsertCount - 1 - relativeIndex;
        QpackPrimitives.ThrowIf(
            !table.Contains(absoluteIndex),
            EncoderStreamError,
            $"relative index {relativeIndex} names no entry in the dynamic table");
        return table.Get(absoluteIndex);
    }

    private void Insert(HeaderField field)
    {
        QpackPrimitives.ThrowIf(
            field.Size > table.Capacity,
            EncoderStreamError,
            $"an entry of size {field.Size} exceeds the dynamic table capacity {table.Capacity}");
        table.Insert(field);
    }
}
