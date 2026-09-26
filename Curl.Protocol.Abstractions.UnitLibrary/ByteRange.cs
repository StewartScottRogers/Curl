namespace Curl.Protocol.Abstractions;

/// <summary>
/// One byte range requested with <c>-r</c>/<c>--range</c>, in one of curl's three
/// forms.
/// </summary>
/// <remarks>
/// <para>
/// The three forms are <c>0-4</c> (an explicit first and last byte,
/// <see cref="Bounded(long, long)" />), <c>6-</c> (from a first byte to the end,
/// <see cref="FromOffset(long)" />) and <c>-5</c> (the last five bytes,
/// <see cref="Suffix(long)" />). <see cref="Kind" /> says which, so the three stay
/// distinguishable: <c>-5</c> is not <c>5-</c>, and neither is a range with a missing
/// half.
/// </para>
/// <para>
/// Both positions are inclusive, as in HTTP's <c>Range</c> header and in curl's own
/// argument, so <c>0-4</c> is five bytes.
/// </para>
/// <para>
/// curl accepts a comma-separated list of ranges, but honours only the first for
/// <c>file://</c>, where there is no multipart response to carry the rest. Parsing the
/// list is the command-line layer's job; a handler receives at most one
/// <see cref="ByteRange" />.
/// </para>
/// </remarks>
public sealed record ByteRange
{
    private ByteRange(
        ByteRangeKind kind,
        long? firstBytePosition,
        long? lastBytePosition,
        long? suffixLength)
    {
        Kind = kind;
        FirstBytePosition = firstBytePosition;
        LastBytePosition = lastBytePosition;
        SuffixLength = suffixLength;
    }

    /// <summary>
    /// Gets which of the three forms this range expresses.
    /// </summary>
    public ByteRangeKind Kind { get; }

    /// <summary>
    /// Gets the inclusive first byte position to seek to, or <see langword="null" /> for
    /// <see cref="ByteRangeKind.Suffix" />, where the start depends on the length of the
    /// resource.
    /// </summary>
    public long? FirstBytePosition { get; }

    /// <summary>
    /// Gets the inclusive last byte position to stop after, or <see langword="null" />
    /// when the range runs to the end of the resource, as it does for
    /// <see cref="ByteRangeKind.FromOffset" /> and <see cref="ByteRangeKind.Suffix" />.
    /// </summary>
    public long? LastBytePosition { get; }

    /// <summary>
    /// Gets the number of trailing bytes requested, or <see langword="null" /> unless
    /// <see cref="Kind" /> is <see cref="ByteRangeKind.Suffix" />. A resource shorter
    /// than this is returned whole, as curl does.
    /// </summary>
    public long? SuffixLength { get; }

    /// <summary>
    /// Creates the <c>first-last</c> form, such as <c>0-4</c>.
    /// </summary>
    /// <param name="firstBytePosition">The inclusive first byte position.</param>
    /// <param name="lastBytePosition">The inclusive last byte position.</param>
    /// <returns>A range whose <see cref="Kind" /> is <see cref="ByteRangeKind.Bounded" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="firstBytePosition" /> is negative, or
    /// <paramref name="lastBytePosition" /> is before it.
    /// </exception>
    public static ByteRange Bounded(long firstBytePosition, long lastBytePosition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstBytePosition);
        ArgumentOutOfRangeException.ThrowIfLessThan(lastBytePosition, firstBytePosition);

        return new ByteRange(
            ByteRangeKind.Bounded,
            firstBytePosition,
            lastBytePosition,
            null);
    }

    /// <summary>
    /// Creates the <c>first-</c> form, such as <c>6-</c>.
    /// </summary>
    /// <param name="firstBytePosition">The inclusive first byte position.</param>
    /// <returns>A range whose <see cref="Kind" /> is <see cref="ByteRangeKind.FromOffset" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="firstBytePosition" /> is negative.
    /// </exception>
    public static ByteRange FromOffset(long firstBytePosition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(firstBytePosition);

        return new ByteRange(
            ByteRangeKind.FromOffset,
            firstBytePosition,
            null,
            null);
    }

    /// <summary>
    /// Creates the <c>-last</c> form, such as <c>-5</c> for the last five bytes.
    /// </summary>
    /// <param name="suffixLength">How many trailing bytes are wanted.</param>
    /// <returns>A range whose <see cref="Kind" /> is <see cref="ByteRangeKind.Suffix" />.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="suffixLength" /> is zero or negative; a suffix of no bytes is not
    /// a range.
    /// </exception>
    public static ByteRange Suffix(long suffixLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(suffixLength);

        return new ByteRange(
            ByteRangeKind.Suffix,
            null,
            null,
            suffixLength);
    }
}
