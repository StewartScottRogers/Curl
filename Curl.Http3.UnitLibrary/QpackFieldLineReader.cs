using Curl.Http2;

namespace Curl.Http3;

/// <summary>
/// Reads the field lines of one encoded field section (RFC 9204 section 4.5.2 to 4.5.6)
/// after its prefix, resolving each against the static table and the decoder's dynamic
/// table and holding every dynamic reference below the section's Required Insert Count.
/// </summary>
internal ref struct QpackFieldLineReader
{
    private const QpackErrorCode Error = QpackErrorCode.DecompressionFailed;

    private readonly ReadOnlySpan<byte> section;
    private readonly QpackDynamicTable table;
    private readonly long requiredInsertCount;
    private readonly long baseIndex;
    private int position;
    private long largestReferencePlusOne;

    /// <summary>
    /// Initializes a new instance of the <see cref="QpackFieldLineReader" /> struct.
    /// </summary>
    /// <param name="section">The encoded field section.</param>
    /// <param name="position">Where its first field line starts.</param>
    /// <param name="table">The decoder's dynamic table.</param>
    /// <param name="requiredInsertCount">The section's Required Insert Count.</param>
    /// <param name="baseIndex">The section's Base.</param>
    public QpackFieldLineReader(ReadOnlySpan<byte> section, int position, QpackDynamicTable table, long requiredInsertCount, long baseIndex)
    {
        this.section = section;
        this.position = position;
        this.table = table;
        this.requiredInsertCount = requiredInsertCount;
        this.baseIndex = baseIndex;
    }

    /// <summary>
    /// Reads every field line to the end of the section.
    /// </summary>
    /// <returns>The field lines, in order.</returns>
    /// <exception cref="QpackException">
    /// A field line is invalid, or the section never references the entry its Required
    /// Insert Count promises (<see cref="QpackErrorCode.DecompressionFailed" />).
    /// </exception>
    /// <exception cref="QpackIncompleteInstructionException">The section ends inside a field line.</exception>
    public List<HeaderField> ReadAll()
    {
        var fields = new List<HeaderField>();
        while (position < section.Length)
        {
            fields.Add(ReadFieldLine());
        }

        QpackPrimitives.ThrowIf(
            largestReferencePlusOne != requiredInsertCount,
            Error,
            "the Required Insert Count is larger than the section's references need");
        return fields;
    }

    private HeaderField ReadFieldLine()
    {
        var first = section[position];
        return first switch
        {
            >= 0x80 => ReadIndexed((first & 0x40) != 0),
            >= 0x40 => ReadLiteralWithNameReference((first & 0x10) != 0, (first & 0x20) != 0),
            >= 0x20 => ReadLiteralWithLiteralName((first & 0x10) != 0),
            >= 0x10 => ReadDynamic(baseIndex + ReadInteger(4)),
            _ => ReadLiteralWithPostBaseNameReference((first & 0x08) != 0),
        };
    }

    private HeaderField ReadIndexed(bool isStatic)
    {
        var index = ReadInteger(6);
        return isStatic ? QpackStaticTable.Get(index, Error) : ReadDynamic(baseIndex - 1 - index);
    }

    private HeaderField ReadLiteralWithNameReference(bool isStatic, bool isNeverIndexed)
    {
        var index = ReadInteger(4);
        var name = isStatic ? QpackStaticTable.Get(index, Error).Name : ReadDynamic(baseIndex - 1 - index).Name;
        return new HeaderField(name, ReadString(7), isNeverIndexed);
    }

    private HeaderField ReadLiteralWithPostBaseNameReference(bool isNeverIndexed)
    {
        var name = ReadDynamic(baseIndex + ReadInteger(3)).Name;
        return new HeaderField(name, ReadString(7), isNeverIndexed);
    }

    private HeaderField ReadLiteralWithLiteralName(bool isNeverIndexed)
    {
        var name = ReadString(3);
        return new HeaderField(name, ReadString(7), isNeverIndexed);
    }

    private HeaderField ReadDynamic(long absoluteIndex)
    {
        QpackPrimitives.ThrowIf(
            absoluteIndex >= requiredInsertCount || !table.Contains(absoluteIndex),
            Error,
            $"dynamic table index {absoluteIndex} is outside the entries the section may reference");
        largestReferencePlusOne = Math.Max(largestReferencePlusOne, absoluteIndex + 1);
        return table.Get(absoluteIndex);
    }

    private long ReadInteger(int prefixBits) => QpackPrimitives.ReadInteger(section, ref position, prefixBits, Error);

    private string ReadString(int prefixBits) => QpackPrimitives.ReadString(section, ref position, prefixBits, Error);
}
