using static Curl.Http2.HpackPrimitives;

namespace Curl.Http2;

/// <summary>
/// Decodes HPACK header blocks (RFC 7541) into header lists. One decoder serves one
/// connection's direction: its dynamic table carries over from block to block, so blocks
/// must be decoded in the order they arrived.
/// </summary>
public sealed class HpackDecoder
{
    /// <summary>The dynamic table size HTTP/2 starts with (RFC 9113 section 6.5.2).</summary>
    public const int DefaultMaximumTableSize = 4096;

    private readonly HpackDynamicTable dynamicTable;

    /// <summary>The largest size a dynamic table size update may set: the local SETTINGS_HEADER_TABLE_SIZE.</summary>
    private int allowedMaximumTableSize;

    /// <summary>Whether the next block must open with a size update, after the allowed size was lowered.</summary>
    private bool isTableSizeUpdateRequired;

    /// <summary>
    /// Initializes a new instance of the <see cref="HpackDecoder" /> class.
    /// </summary>
    /// <param name="maximumTableSize">
    /// The largest dynamic table the peer may use: the SETTINGS_HEADER_TABLE_SIZE this
    /// endpoint advertises. The table starts at this size.
    /// </param>
    public HpackDecoder(int maximumTableSize = DefaultMaximumTableSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTableSize);
        allowedMaximumTableSize = maximumTableSize;
        dynamicTable = new HpackDynamicTable(maximumTableSize);
    }

    /// <summary>Gets the dynamic table's current maximum size, as last set by the peer.</summary>
    public int MaximumTableSize => dynamicTable.MaximumSize;

    /// <summary>Gets the dynamic table's current size (RFC 7541 section 4.1).</summary>
    public int TableSize => dynamicTable.Size;

    /// <summary>
    /// Changes the largest dynamic table the peer may use, as when this endpoint sends a new
    /// SETTINGS_HEADER_TABLE_SIZE. Lowering it below the table's current maximum means the
    /// next block must open with a size update within it (RFC 7541 section 4.2).
    /// </summary>
    /// <param name="maximumTableSize">The new limit in bytes.</param>
    public void SetAllowedMaximumTableSize(int maximumTableSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTableSize);
        allowedMaximumTableSize = maximumTableSize;
        isTableSizeUpdateRequired |= maximumTableSize < dynamicTable.MaximumSize;
    }

    /// <summary>
    /// Decodes one complete header block: a HEADERS frame's fragment with all its
    /// CONTINUATION fragments appended.
    /// </summary>
    /// <param name="block">The header block.</param>
    /// <returns>The header list, in order.</returns>
    /// <exception cref="HpackDecodingException">The block breaks a rule of RFC 7541; <see cref="HpackDecodingException.Error" /> says which.</exception>
    public IReadOnlyList<HeaderField> Decode(ReadOnlySpan<byte> block)
    {
        var fields = new List<HeaderField>();
        var position = 0;
        ReadTableSizeUpdates(block, ref position);
        while (position < block.Length)
        {
            fields.Add(ReadField(block, ref position));
        }

        return fields;
    }

    private void ReadTableSizeUpdates(ReadOnlySpan<byte> block, ref int position)
    {
        while (position < block.Length && (block[position] & 0xE0) == 0x20)
        {
            var size = ReadInteger(block, ref position, 5);
            ThrowIf(size > allowedMaximumTableSize, HpackDecodingError.TableSizeUpdateTooLarge);
            dynamicTable.Resize(size);
            isTableSizeUpdateRequired = false;
        }

        ThrowIf(isTableSizeUpdateRequired, HpackDecodingError.TableSizeUpdateMissing);
    }

    private HeaderField ReadField(ReadOnlySpan<byte> block, ref int position)
    {
        var first = block[position];
        if ((first & 0x80) != 0)
        {
            return GetIndexed(ReadInteger(block, ref position, 7));
        }

        if ((first & 0x40) != 0)
        {
            var field = ReadLiteral(block, ref position, 6, isNeverIndexed: false);
            dynamicTable.Add(field);
            return field;
        }

        ThrowIf((first & 0x20) != 0, HpackDecodingError.TableSizeUpdateNotAtStart);
        return ReadLiteral(block, ref position, 4, isNeverIndexed: (first & 0x10) != 0);
    }

    private HeaderField ReadLiteral(ReadOnlySpan<byte> block, ref int position, int prefixBits, bool isNeverIndexed)
    {
        var nameIndex = ReadInteger(block, ref position, prefixBits);
        var name = nameIndex == 0 ? ReadString(block, ref position) : GetIndexed(nameIndex).Name;
        var value = ReadString(block, ref position);
        return new HeaderField(name, value, isNeverIndexed);
    }

    private HeaderField GetIndexed(int index)
    {
        ThrowIf(index == 0 || index > HpackStaticTable.Count + dynamicTable.Count, HpackDecodingError.InvalidIndex);
        return index <= HpackStaticTable.Count
            ? HpackStaticTable.Get(index)
            : dynamicTable.Get(index - HpackStaticTable.Count);
    }
}
