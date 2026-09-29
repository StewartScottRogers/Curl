using Curl.Http2;

namespace Curl.Http3;

/// <summary>
/// A QPACK dynamic table (RFC 9204 section 3.2): entries addressed by absolute index, the
/// first inserted being 0, evicted oldest first to stay within the capacity. The encoder
/// and the decoder each keep one; neither checks here whether an eviction is allowed.
/// </summary>
internal sealed class QpackDynamicTable
{
    private readonly List<HeaderField> entries = [];

    /// <summary>Gets the capacity the table's size is held to.</summary>
    public long Capacity { get; private set; }

    /// <summary>Gets the sum of the entries' sizes (RFC 9204 section 3.2.1).</summary>
    public long Size { get; private set; }

    /// <summary>Gets how many entries have ever been inserted, the Insert Count.</summary>
    public long InsertCount { get; private set; }

    /// <summary>Gets how many entries have been evicted, which is the oldest entry's absolute index.</summary>
    public long DroppedCount => InsertCount - entries.Count;

    /// <summary>
    /// Gets whether an entry with <paramref name="absoluteIndex" /> is in the table.
    /// </summary>
    /// <param name="absoluteIndex">The absolute index, which may be negative.</param>
    /// <returns>Whether it has been inserted and not evicted.</returns>
    public bool Contains(long absoluteIndex) => absoluteIndex >= DroppedCount && absoluteIndex < InsertCount;

    /// <summary>
    /// Gets the entry at <paramref name="absoluteIndex" />, which <see cref="Contains" /> holds for.
    /// </summary>
    /// <param name="absoluteIndex">The absolute index.</param>
    /// <returns>The entry.</returns>
    public HeaderField Get(long absoluteIndex) => entries[(int)(absoluteIndex - DroppedCount)];

    /// <summary>
    /// Gets what <see cref="DroppedCount" /> would become if the oldest entries were evicted
    /// until <paramref name="incomingSize" /> more fits within <paramref name="capacity" />.
    /// </summary>
    /// <param name="incomingSize">The size of the entry to be inserted, or 0.</param>
    /// <param name="capacity">The capacity to fit within.</param>
    /// <returns>The dropped count after evicting.</returns>
    public long DroppedCountAfterFitting(long incomingSize, long capacity)
    {
        var size = Size;
        var dropped = DroppedCount;
        foreach (var entry in entries)
        {
            if (size + incomingSize <= capacity)
            {
                break;
            }

            size -= entry.Size;
            dropped++;
        }

        return dropped;
    }

    /// <summary>
    /// Changes the capacity, evicting the oldest entries until the size fits it.
    /// </summary>
    /// <param name="capacity">The new capacity.</param>
    public void SetCapacity(long capacity)
    {
        Capacity = capacity;
        EvictToFit(0);
    }

    /// <summary>
    /// Inserts <paramref name="field" /> as the newest entry, first evicting the oldest
    /// entries until it fits. The caller has checked that its size is within the capacity.
    /// </summary>
    /// <param name="field">The entry; <see cref="HeaderField.IsNeverIndexed" /> is ignored.</param>
    public void Insert(HeaderField field)
    {
        var entry = new HeaderField(field.Name, field.Value);
        EvictToFit(entry.Size);
        entries.Add(entry);
        Size += entry.Size;
        InsertCount++;
    }

    /// <summary>
    /// Finds the newest entry with <paramref name="name" /> and, when
    /// <paramref name="matchValue" /> is set, <paramref name="value" />, at or below
    /// <paramref name="largestUsableIndex" />.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="value">The field value.</param>
    /// <param name="matchValue">Whether the value has to match too.</param>
    /// <param name="largestUsableIndex">The largest absolute index the caller may reference.</param>
    /// <returns>The absolute index, or -1 when no usable entry matches.</returns>
    public long FindNewest(string name, string value, bool matchValue, long largestUsableIndex)
    {
        for (var absoluteIndex = Math.Min(largestUsableIndex, InsertCount - 1); absoluteIndex >= DroppedCount; absoluteIndex--)
        {
            var entry = Get(absoluteIndex);
            if (entry.Name == name && (!matchValue || entry.Value == value))
            {
                return absoluteIndex;
            }
        }

        return -1;
    }

    private void EvictToFit(long incomingSize)
    {
        while (Size + incomingSize > Capacity)
        {
            Size -= entries[0].Size;
            entries.RemoveAt(0);
        }
    }
}
