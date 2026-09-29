namespace Curl.Http3;

/// <summary>
/// The Required Insert Count's wire encoding (RFC 9204 section 4.5.1.1): the count modulo
/// twice the most entries the decoder's table can hold, plus one, so it stays small on a
/// long-lived connection.
/// </summary>
internal static class QpackRequiredInsertCount
{
    /// <summary>The per-entry overhead of RFC 9204 section 3.2.1, which bounds how many entries fit.</summary>
    public const long EntryOverhead = 32;

    /// <summary>
    /// Gets MaxEntries: how many entries a table of <paramref name="maximumTableCapacity" /> can hold at most.
    /// </summary>
    /// <param name="maximumTableCapacity">The decoder's SETTINGS_QPACK_MAX_TABLE_CAPACITY.</param>
    /// <returns>The capacity divided by 32, rounded down.</returns>
    public static long GetMaximumEntries(long maximumTableCapacity) => maximumTableCapacity / EntryOverhead;

    /// <summary>
    /// Encodes a Required Insert Count.
    /// </summary>
    /// <param name="requiredInsertCount">The Required Insert Count.</param>
    /// <param name="maximumEntries">MaxEntries, from <see cref="GetMaximumEntries" />.</param>
    /// <returns>The Encoded Insert Count.</returns>
    public static long Encode(long requiredInsertCount, long maximumEntries) =>
        requiredInsertCount == 0 ? 0 : (requiredInsertCount % (2 * maximumEntries)) + 1;

    /// <summary>
    /// Decodes an Encoded Insert Count against the decoder's Insert Count.
    /// </summary>
    /// <param name="encodedInsertCount">The Encoded Insert Count from the field section prefix.</param>
    /// <param name="maximumEntries">MaxEntries, from <see cref="GetMaximumEntries" />.</param>
    /// <param name="totalInsertCount">How many entries the decoder has inserted.</param>
    /// <returns>The Required Insert Count.</returns>
    /// <exception cref="QpackException">
    /// The encoding is not one any Required Insert Count within reach could have produced
    /// (<see cref="QpackErrorCode.DecompressionFailed" />).
    /// </exception>
    public static long Decode(long encodedInsertCount, long maximumEntries, long totalInsertCount)
    {
        if (encodedInsertCount == 0)
        {
            return 0;
        }

        var fullRange = 2 * maximumEntries;
        ThrowIfInvalid(encodedInsertCount > fullRange);
        var maximumValue = totalInsertCount + maximumEntries;
        var maximumWrapped = maximumValue / fullRange * fullRange;
        var requiredInsertCount = maximumWrapped + encodedInsertCount - 1;
        if (requiredInsertCount > maximumValue)
        {
            ThrowIfInvalid(requiredInsertCount <= fullRange);
            requiredInsertCount -= fullRange;
        }

        ThrowIfInvalid(requiredInsertCount == 0);
        return requiredInsertCount;
    }

    private static void ThrowIfInvalid(bool condition) =>
        QpackPrimitives.ThrowIf(condition, QpackErrorCode.DecompressionFailed, "the Required Insert Count is beyond the dynamic table");
}
